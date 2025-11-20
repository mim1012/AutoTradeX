using AutoTrader.Core.Configuration;
using AutoTrader.Core.DTOs.WebSocket;
using AutoTrader.Core.Models.Realtime;
using AutoTrader.Core.Models.Trading;
using AutoTrader.Core.Models.WebSocket;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Services.Api;
using AutoTrader.Core.Services.Market;
using AutoTrader.Core.Services.Realtime;
using AutoTrader.Core.Services.Stock;
using AutoTrader.Core.Services.Trading;
using AutoTrader.Core.Services.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AutoTrader.Worker;

/// <summary>
/// 메인 거래 워커 서비스
/// - WebSocket 실시간 모니터링
/// - 조건 평가 및 후보 추적
/// - 자동 주문 실행
/// </summary>
public class TradingWorker : BackgroundService
{
    private readonly ITop300StockService _stockService;
    private readonly IHistoricalDataService _historicalDataService;
    private readonly IWebSocketManager _webSocketManager;
    private readonly IRealtimeDataAggregator _dataAggregator;
    private readonly ISnapshotDataService _snapshotService;
    private readonly IMultiPointValidator _multiPointValidator;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly ICandidateTracker _candidateTracker;
    private readonly IOrderExecutor _orderExecutor;
    private readonly BalanceApiService _balanceApiService;
    private readonly TradingSettings _tradingSettings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TradingWorker> _logger;

    // 거래 조건 (DB에서 로드)
    private CompositeCondition _tradingCondition = null!;

    // 활성 계좌 정보 (DB에서 로드)
    private Core.Models.Database.Account? _activeAccount;

    // 조건식 마지막 업데이트 시간 (변경 감지용)
    private DateTime _lastConditionUpdateTime = DateTime.MinValue;

    // 주문 실행 완료 플래그
    private bool _ordersExecutedToday = false;
    private DateTime _lastOrderExecutionDate = DateTime.MinValue;

    // Top 300 종목 캐시 (스냅샷 방식용)
    private List<string> _top300Symbols = new();

    // Heartbeat 타이머 (30초마다)
    private Timer? _heartbeatTimer;

    public TradingWorker(
        ITop300StockService stockService,
        IHistoricalDataService historicalDataService,
        IWebSocketManager webSocketManager,
        IRealtimeDataAggregator dataAggregator,
        ISnapshotDataService snapshotService,
        IMultiPointValidator multiPointValidator,
        IConditionEvaluator conditionEvaluator,
        ICandidateTracker candidateTracker,
        IOrderExecutor orderExecutor,
        BalanceApiService balanceApiService,
        IOptions<TradingSettings> tradingSettings,
        IServiceScopeFactory scopeFactory,
        ILogger<TradingWorker> logger)
    {
        _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
        _historicalDataService = historicalDataService ?? throw new ArgumentNullException(nameof(historicalDataService));
        _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
        _dataAggregator = dataAggregator ?? throw new ArgumentNullException(nameof(dataAggregator));
        _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        _multiPointValidator = multiPointValidator ?? throw new ArgumentNullException(nameof(multiPointValidator));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        _candidateTracker = candidateTracker ?? throw new ArgumentNullException(nameof(candidateTracker));
        _orderExecutor = orderExecutor ?? throw new ArgumentNullException(nameof(orderExecutor));
        _balanceApiService = balanceApiService ?? throw new ArgumentNullException(nameof(balanceApiService));
        _tradingSettings = tradingSettings?.Value ?? throw new ArgumentNullException(nameof(tradingSettings));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // WebSocket 데이터 수신 이벤트는 조건부 연결
        if (_tradingSettings.UseRealtimeMonitoring)
        {
            _webSocketManager.RealtimeDataReceived += OnRealtimeDataReceived;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TradingWorker started");

        try
        {
            // DB 상태를 "Running"으로 설정
            await UpdateWorkerStatusAsync(isRunning: true, lastLog: "Worker started");

            // Heartbeat 타이머 시작 (30초마다)
            _heartbeatTimer = new Timer(
                async _ => await SendHeartbeatAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));

            // 초기화
            await InitializeAsync(stoppingToken);

            // 메인 루프
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. 조건식 변경 감지 (60초마다 체크)
                    await CheckAndReloadConditionsAsync();

                    // 2. 실시간 모니터링 vs 스냅샷 방식 분기
                    if (_tradingSettings.UseRealtimeMonitoring)
                    {
                        // 기존 실시간 방식
                        await EvaluateConditionsAsync();
                        _candidateTracker.RemoveExpiredCandidates();
                    }
                    else
                    {
                        // 새로운 스냅샷 방식
                        var currentEt = GetEasternTime();
                        var currentTimeStr = currentEt.ToString("HH:mm");

                        // 디버그: 현재 시간과 스캔 시간 목록 출력
                        _logger.LogDebug("Current ET time: {CurrentTime}, Scan times: [{ScanTimes}], Orders executed: {OrdersExecuted}",
                            currentTimeStr,
                            string.Join(", ", _tradingSettings.ScanTimes),
                            _ordersExecutedToday);

                        // 스캔 시간 체크
                        if (_tradingSettings.ScanTimes.Contains(currentTimeStr) && !_ordersExecutedToday)
                        {
                            _logger.LogInformation("Scan time detected: {Time}", currentTimeStr);
                            await ExecuteScanAsync();
                        }
                    }

                    // 3. 자정 리셋 체크
                    CheckDailyReset();

                    // 4. 주문 실행 체크 (15:40 ET)
                    await CheckAndExecuteOrdersAsync();

                    // 5. DB 상태 업데이트 (통계)
                    await UpdateWorkerMetricsAsync();

                    // 6. 대기 (실시간: 10초, 스냅샷: 1분)
                    var delay = _tradingSettings.UseRealtimeMonitoring
                        ? TimeSpan.FromSeconds(10)
                        : TimeSpan.FromMinutes(1);
                    await Task.Delay(delay, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in main loop");
                    await UpdateWorkerStatusAsync(isRunning: true, lastLog: $"Error: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TradingWorker cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in TradingWorker");
            await UpdateWorkerStatusAsync(isRunning: false, lastLog: $"Fatal error: {ex.Message}");
            throw;
        }
        finally
        {
            _heartbeatTimer?.Dispose();
            await UpdateWorkerStatusAsync(isRunning: false, lastLog: "Worker stopped");
            await CleanupAsync();
        }
    }

    /// <summary>
    /// 초기화
    /// </summary>
    private async Task InitializeAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Initializing TradingWorker...");

        // 1. DB에서 활성 계좌 로드
        await LoadActiveAccountAsync();

        if (_activeAccount == null)
        {
            _logger.LogError("No active account found in database. Worker cannot start.");
            await UpdateWorkerStatusAsync(isRunning: false, lastLog: "No active account configured");
            throw new InvalidOperationException("No active account found in database");
        }

        _logger.LogInformation("Active account loaded: {AccountNumber} ({AccountName})",
            _activeAccount.AccountNumber, _activeAccount.AccountName);

        // 2. DB에서 조건식 로드 및 변환
        await LoadTradingConditionAsync();

        if (_tradingCondition == null || _tradingCondition.Conditions.Count == 0)
        {
            _logger.LogError("No trading conditions found for active account. Worker cannot start.");
            await UpdateWorkerStatusAsync(isRunning: false, lastLog: "No trading conditions configured");
            throw new InvalidOperationException("No trading conditions found");
        }

        _logger.LogInformation("Trading conditions loaded: {ConditionName} with {Count} conditions",
            _tradingCondition.Name, _tradingCondition.Conditions.Count);

        // 조건식 업데이트 시간 저장
        _lastConditionUpdateTime = DateTime.UtcNow;

        // Top 300 종목 초기 로드
        await _stockService.RefreshTop300Async();
        var top300 = _stockService.GetCachedTop300();
        _top300Symbols = top300.Select(s => s.Symbol).ToList();

        _logger.LogInformation("Top 300 stocks loaded: {Count} stocks", _top300Symbols.Count);

        // 과거 캔들 데이터 로드 (MovingAverage, PriceComparison 조건용)
        _logger.LogInformation("Loading historical data for {Count} stocks...", _top300Symbols.Count);
        await _historicalDataService.LoadHistoricalDataAsync(_top300Symbols);
        _logger.LogInformation("Historical data loaded: {Count} stocks cached",
            _historicalDataService.GetCachedSymbolCount());

        // WebSocket은 조건부 시작
        if (_tradingSettings.UseRealtimeMonitoring)
        {
            await _webSocketManager.StartAllSessionsAsync(top300);
            _logger.LogInformation("WebSocket real-time monitoring enabled: {Count} stocks subscribed",
                _webSocketManager.TotalSubscribedStockCount);
        }
        else
        {
            _logger.LogInformation("Snapshot-based scanning enabled");
            _logger.LogInformation("Scan schedule: {ScanTimes}", string.Join(", ", _tradingSettings.ScanTimes));
            _logger.LogInformation("Required scan matches: {Required}/{Total}",
                _tradingSettings.RequiredScanMatches, _tradingSettings.ScanTimes.Count);
        }

        _logger.LogInformation("TradingWorker initialization complete");
    }

    /// <summary>
    /// 실시간 데이터 수신 이벤트 핸들러
    /// </summary>
    private void OnRealtimeDataReceived(object? sender, RealtimeDataReceivedEventArgs e)
    {
        // 데이터 집계기에 업데이트
        _dataAggregator.UpdateData(e.Data);
    }

    /// <summary>
    /// 조건 평가 및 후보 추적
    /// </summary>
    private async Task EvaluateConditionsAsync()
    {
        // 신선한 데이터만 조회 (10초 이내)
        var freshData = _dataAggregator.GetFreshData();

        if (freshData.Count == 0)
        {
            _logger.LogDebug("No fresh data available for evaluation");
            return;
        }

        // 조건 평가 (병렬 처리)
        var matchedStocks = _conditionEvaluator.EvaluateAllStocks(_tradingCondition, freshData);

        if (matchedStocks.Count > 0)
        {
            _logger.LogInformation("Condition matched: {Count} stocks", matchedStocks.Count);

            // 후보 추적 (2회 확인 로직)
            _candidateTracker.TrackCandidates(matchedStocks);

            // 확정된 후보 로깅
            var confirmedCandidates = _candidateTracker.GetConfirmedCandidates();
            if (confirmedCandidates.Count > 0)
            {
                _logger.LogInformation("Confirmed candidates: {Candidates}",
                    string.Join(", ", confirmedCandidates.Select(c => c.ToString())));
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 주문 실행 체크 및 실행
    /// </summary>
    private async Task CheckAndExecuteOrdersAsync()
    {
        // 오늘 이미 주문 실행했는지 확인
        var today = DateTime.UtcNow.Date;
        if (_lastOrderExecutionDate.Date == today && _ordersExecutedToday)
        {
            return; // 오늘 이미 주문 실행함
        }

        // 주문 시간 체크 (15:50 ET ~ 16:00 ET)
        if (!_orderExecutor.IsOrderTimeWindow())
        {
            var secondsUntil = _orderExecutor.SecondsUntilOrderTime();
            if (secondsUntil > 0 && secondsUntil < 60)
            {
                _logger.LogInformation("Order time approaching in {Seconds} seconds", secondsUntil);
            }
            return;
        }

        _logger.LogInformation("Order time window detected - preparing orders");

        // 후보 조회 방식 분기
        List<CandidateStock> confirmedCandidates;

        if (_tradingSettings.UseRealtimeMonitoring)
        {
            // 기존 방식: CandidateTracker에서 조회
            confirmedCandidates = _candidateTracker.GetConfirmedCandidates();
        }
        else
        {
            // 새로운 방식: MultiPointValidator에서 조회
            var multiPointResult = _multiPointValidator.GetFinalCandidates(
                _tradingSettings.RequiredScanMatches);

            _logger.LogInformation("Multi-point validation result: {Result}", multiPointResult);

            confirmedCandidates = multiPointResult.FinalCandidates;

            // 스캔 결과 초기화
            _multiPointValidator.ClearResults();
        }

        if (confirmedCandidates.Count == 0)
        {
            _logger.LogWarning("No confirmed candidates for order execution");
            _ordersExecutedToday = true;
            _lastOrderExecutionDate = DateTime.UtcNow;
            return;
        }

        // 실제 계좌 잔고 조회
        decimal accountBalance;
        try
        {
            accountBalance = await _balanceApiService.GetAvailableBalanceAsync(_activeAccount.AccountNumber);

            if (accountBalance <= 0)
            {
                _logger.LogWarning("Insufficient balance for order execution: ${Balance}", accountBalance);
                _ordersExecutedToday = true;
                _lastOrderExecutionDate = DateTime.UtcNow;
                return;
            }

            _logger.LogInformation("Available balance for trading: ${Balance:N2}", accountBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get account balance. Skipping order execution.");
            return;
        }

        // 주문 계획 생성 (조건 충족 종목 개수에 따라 투자 비율 결정)
        var orderPlans = new List<OrderExecutionPlan>();

        if (confirmedCandidates.Count == 1)
        {
            // 1개 종목: 전체 금액 100% 투자
            var candidate = confirmedCandidates[0];

            _logger.LogInformation("Single candidate detected - investing 100% in {Symbol}", candidate.Symbol);

            var plan = _orderExecutor.CreateOrderPlan(
                candidate,
                accountBalance,
                allocationPercent: 1.0m); // 100% 투자

            orderPlans.Add(plan);
        }
        else if (confirmedCandidates.Count >= 2)
        {
            // 2개 이상: 하락률 상위 2개 선정, 각 50% 투자
            var top2 = confirmedCandidates
                .OrderBy(c => c.CurrentChangeRate) // 하락률 높은 순
                .Take(2)
                .ToList();

            _logger.LogInformation("Multiple candidates detected - investing 50% each in top 2 declining stocks: {Stocks}",
                string.Join(", ", top2.Select(s => $"{s.Symbol} ({s.CurrentChangeRate:P2})")));

            foreach (var candidate in top2)
            {
                var plan = _orderExecutor.CreateOrderPlan(
                    candidate,
                    accountBalance,
                    allocationPercent: 0.5m); // 50% 투자

                orderPlans.Add(plan);
            }
        }

        // 주문 실행
        var results = await _orderExecutor.ExecuteOrdersAsync(orderPlans);

        // 결과 로깅
        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                _logger.LogInformation("Order SUCCESS: {Result}", result);
            }
            else
            {
                _logger.LogError("Order FAILED: {Result}", result);
            }
        }

        // 주문 실행 완료 플래그 설정
        _ordersExecutedToday = true;
        _lastOrderExecutionDate = DateTime.UtcNow;

        _logger.LogInformation("Order execution complete for today");
    }

    /// <summary>
    /// 정리
    /// </summary>
    private async Task CleanupAsync()
    {
        _logger.LogInformation("Cleaning up TradingWorker...");

        // WebSocket 중지
        await _webSocketManager.StopAllSessionsAsync();

        _logger.LogInformation("TradingWorker cleanup complete");
    }

    /// <summary>
    /// Worker 상태 업데이트 (DB)
    /// </summary>
    private async Task UpdateWorkerStatusAsync(bool isRunning, string? lastLog = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var workerStatusRepo = scope.ServiceProvider.GetRequiredService<WorkerStatusRepository>();

            await workerStatusRepo.SetRunningStatusAsync(isRunning, lastLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update worker status in DB");
        }
    }

    /// <summary>
    /// Worker 통계 업데이트 (DB)
    /// </summary>
    private async Task UpdateWorkerMetricsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var workerStatusRepo = scope.ServiceProvider.GetRequiredService<WorkerStatusRepository>();

            var top300Count = _stockService.GetCachedTop300().Count;
            var candidateCount = _candidateTracker.GetConfirmedCandidates().Count;
            var orderCount = _ordersExecutedToday ? _tradingSettings.MaxStocksToTrade : 0;

            await workerStatusRepo.UpdateMetricsAsync(top300Count, candidateCount, orderCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update worker metrics in DB");
        }
    }

    /// <summary>
    /// Heartbeat 전송 (30초마다)
    /// </summary>
    private async Task SendHeartbeatAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var workerStatusRepo = scope.ServiceProvider.GetRequiredService<WorkerStatusRepository>();

            await workerStatusRepo.UpdateHeartbeatAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send heartbeat");
        }
    }

    /// <summary>
    /// DB에서 활성 계좌 로드
    /// </summary>
    private async Task LoadActiveAccountAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var accountRepo = scope.ServiceProvider.GetRequiredService<AccountRepository>();

            _activeAccount = await accountRepo.GetActiveAccountAsync();

            if (_activeAccount == null)
            {
                _logger.LogWarning("No active account found in database");
            }
            else
            {
                _logger.LogInformation("Loaded active account: {AccountNumber} - {AccountName}",
                    _activeAccount.AccountNumber, _activeAccount.AccountName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load active account from database");
            throw;
        }
    }

    /// <summary>
    /// DB에서 조건식 로드 및 TradingCondition으로 변환
    /// </summary>
    private Task LoadTradingConditionAsync()
    {
        try
        {
            if (_activeAccount?.ConditionSet == null || _activeAccount.ConditionSet.Conditions.Count == 0)
            {
                _logger.LogWarning("No condition set found for active account");
                return Task.CompletedTask;
            }

            var conditionSet = _activeAccount.ConditionSet;

            // DB Condition → TradingCondition 변환
            var tradingConditions = new List<TradingCondition>();

            foreach (var dbCondition in conditionSet.Conditions.OrderBy(c => c.ConditionOrder))
            {
                var tradingCondition = ConvertDbConditionToTradingCondition(dbCondition);
                if (tradingCondition != null)
                {
                    tradingConditions.Add(tradingCondition);
                }
            }

            // CompositeCondition 생성
            _tradingCondition = new CompositeCondition
            {
                Name = conditionSet.Name,
                Logic = ConditionLogic.And, // 기본적으로 AND 조합
                Conditions = tradingConditions
            };

            _logger.LogInformation("Loaded {Count} trading conditions from database", tradingConditions.Count);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load trading conditions from database");
            throw;
        }
    }

    /// <summary>
    /// 조건식 변경 감지 및 리로드 (60초마다)
    /// </summary>
    private async Task CheckAndReloadConditionsAsync()
    {
        // 60초마다만 체크
        if ((DateTime.UtcNow - _lastConditionUpdateTime).TotalSeconds < 60)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var accountRepo = scope.ServiceProvider.GetRequiredService<AccountRepository>();

            // DB에서 최신 계좌 및 조건식 조회
            var latestAccount = await accountRepo.GetActiveAccountAsync();

            if (latestAccount == null)
            {
                _logger.LogWarning("Active account no longer exists. Worker will continue with current settings.");
                _lastConditionUpdateTime = DateTime.UtcNow;
                return;
            }

            // ConditionSet UpdatedAt 비교
            if (latestAccount.ConditionSet != null &&
                latestAccount.ConditionSet.UpdatedAt > _lastConditionUpdateTime)
            {
                _logger.LogInformation("Condition set has been updated. Reloading conditions...");

                // 계좌 및 조건식 리로드
                _activeAccount = latestAccount;
                await LoadTradingConditionAsync();

                _logger.LogInformation("Conditions reloaded successfully: {ConditionName} with {Count} conditions",
                    _tradingCondition?.Name ?? "Unknown", _tradingCondition?.Conditions.Count ?? 0);

                await UpdateWorkerStatusAsync(isRunning: true, lastLog: "Conditions reloaded");
            }

            _lastConditionUpdateTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check and reload conditions");
        }
    }

    /// <summary>
    /// DB Condition을 TradingCondition으로 변환
    /// </summary>
    private TradingCondition? ConvertDbConditionToTradingCondition(Core.Models.Database.Condition dbCondition)
    {
        try
        {
            // Parameters JSON 파싱 (예: {"Operator":"LessThan","Value":-3.0})
            var parameters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dbCondition.Parameters);
            if (parameters == null)
            {
                _logger.LogWarning("Failed to parse condition parameters: {Parameters}", dbCondition.Parameters);
                return null;
            }

            // ConditionType 매핑
            var conditionType = dbCondition.ConditionType.ToLower() switch
            {
                "changerate" => ConditionType.ChangeRate,
                "price" => ConditionType.Price,
                "volume" => ConditionType.Volume,
                _ => ConditionType.ChangeRate // 기본값
            };

            // Operator 매핑
            var operatorStr = parameters.ContainsKey("Operator") ? parameters["Operator"].ToString() : "LessThan";
            var conditionOperator = operatorStr switch
            {
                "GreaterThan" => ConditionOperator.GreaterThan,
                "LessThan" => ConditionOperator.LessThan,
                "Equals" => ConditionOperator.Equals,
                _ => ConditionOperator.LessThan // 기본값
            };

            // Value 추출
            decimal value = 0m;
            if (parameters.ContainsKey("Value"))
            {
                var valueObj = parameters["Value"];
                if (valueObj is System.Text.Json.JsonElement jsonElement)
                {
                    value = jsonElement.GetDecimal();
                }
                else
                {
                    value = Convert.ToDecimal(valueObj);
                }
            }

            return new TradingCondition
            {
                Name = $"조건 {dbCondition.ConditionOrder}",
                Type = conditionType,
                Operator = conditionOperator,
                Value = value,
                IsEnabled = true,
                Description = $"{dbCondition.ConditionType} {operatorStr} {value}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert DB condition to TradingCondition: {ConditionId}", dbCondition.ConditionId);
            return null;
        }
    }

    /// <summary>
    /// 스냅샷 기반 스캔 실행
    /// </summary>
    private async Task ExecuteScanAsync()
    {
        try
        {
            _logger.LogInformation("Starting snapshot scan... (Total scans: {Count})",
                _multiPointValidator.ScanCount);

            // Top 300 종목 캐시에서 직접 가져오기 (API 호출 0번!)
            var top300Items = _stockService.GetCachedTop300();

            if (top300Items.Count == 0)
            {
                _logger.LogWarning("No Top 300 stocks available in cache");
                var failedResult = new ScanResult
                {
                    ScanTime = GetEasternTime(),
                    TotalStocksScanned = 0,
                    IsSuccess = false,
                    ErrorMessage = "No cached Top 300 data"
                };
                _multiPointValidator.AddScanResult(failedResult);
                return;
            }

            // TradeRankingItem을 CachedStockData로 변환
            var snapshotData = top300Items.Select(item => new CachedStockData
            {
                Symbol = item.Symbol,
                LatestData = new RealtimeStockData
                {
                    Symbol = item.Symbol,
                    CurrentPrice = item.CurrentPrice.ToString("F2"),
                    ChangeRate = item.ChangePercent.ToString("F2"),
                    PriceDifference = (item.CurrentPrice * item.ChangePercent / 100m).ToString("F2"),
                    AccumulatedTradeAmount = item.TradeAmount.ToString("F0"),
                    ExecutionTime = DateTime.UtcNow.ToString("HHmmss")
                },
                LastUpdatedAt = DateTime.UtcNow,
                UpdateCount = 1
            }).ToList();

            _logger.LogInformation("Snapshot data prepared: {Count} stocks from cache", snapshotData.Count);

            // 조건 평가
            var matchedData = _conditionEvaluator.EvaluateAllStocks(_tradingCondition, snapshotData);

            // CachedStockData를 CandidateStock으로 변환
            var matchedCandidates = matchedData.Select(stockData => new CandidateStock
            {
                Symbol = stockData.Symbol,
                Name = stockData.LatestData?.Symbol ?? stockData.Symbol,
                FirstConfirmedAt = DateTime.UtcNow,
                SecondConfirmedAt = null,
                CurrentChangeRate = stockData.ChangeRate,
                CurrentPrice = stockData.CurrentPrice,
                TradeAmount = stockData.TradeAmount
            }).ToList();

            _logger.LogInformation("Condition evaluation: {Matched}/{Total} stocks matched", matchedCandidates.Count, snapshotData.Count);

            // 스캔 결과 저장
            var scanResult = new ScanResult
            {
                ScanTime = GetEasternTime(),
                MatchedStocks = matchedCandidates,
                TotalStocksScanned = snapshotData.Count(),
                IsSuccess = true
            };

            _multiPointValidator.AddScanResult(scanResult);
            _logger.LogInformation("Scan result added: [{ScanTime}] Matched: {Matched}/{Total} stocks",
                scanResult.ScanTime.ToString("HH:mm:ss"), matchedCandidates.Count, snapshotData.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scan execution");
            var failedResult = new ScanResult
            {
                ScanTime = GetEasternTime(),
                TotalStocksScanned = _top300Symbols.Count,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
            _multiPointValidator.AddScanResult(failedResult);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 자정 리셋 체크
    /// </summary>
    private void CheckDailyReset()
    {
        var today = DateTime.UtcNow.Date;
        if (_lastOrderExecutionDate.Date < today && _ordersExecutedToday)
        {
            _logger.LogInformation("Daily reset: Clearing order execution flag");
            _ordersExecutedToday = false;

            if (!_tradingSettings.UseRealtimeMonitoring)
            {
                _multiPointValidator.ClearResults();
            }
        }
    }

    /// <summary>
    /// 미국 동부 시간 (ET) 계산
    /// </summary>
    private DateTime GetEasternTime()
    {
        try
        {
            var etZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, etZone);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                var etZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, etZone);
            }
            catch (TimeZoneNotFoundException)
            {
                var now = DateTime.UtcNow;
                var isDst = IsDaylightSavingTimePeriod(now);
                var offset = isDst ? -4 : -5;
                return now.AddHours(offset);
            }
        }
    }

    private static bool IsDaylightSavingTimePeriod(DateTime utcNow)
    {
        var year = utcNow.Year;
        var marchSecondSunday = GetNthSunday(year, 3, 2);
        var dstStart = new DateTime(year, 3, marchSecondSunday, 7, 0, 0, DateTimeKind.Utc);
        var novemberFirstSunday = GetNthSunday(year, 11, 1);
        var dstEnd = new DateTime(year, 11, novemberFirstSunday, 6, 0, 0, DateTimeKind.Utc);
        return utcNow >= dstStart && utcNow < dstEnd;
    }

    private static int GetNthSunday(int year, int month, int n)
    {
        var firstDay = new DateTime(year, month, 1);
        var firstSunday = firstDay.Day + ((7 - (int)firstDay.DayOfWeek) % 7);
        if (firstSunday == 0) firstSunday = 7;
        return firstSunday + (n - 1) * 7;
    }

    public override void Dispose()
    {
        _heartbeatTimer?.Dispose();
        if (_tradingSettings.UseRealtimeMonitoring)
        {
            _webSocketManager.RealtimeDataReceived -= OnRealtimeDataReceived;
        }
        base.Dispose();
    }
}
