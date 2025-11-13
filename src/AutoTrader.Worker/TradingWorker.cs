using AutoTrader.Core.Configuration;
using AutoTrader.Core.Models.Trading;
using AutoTrader.Core.Models.WebSocket;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Services.Api;
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
    private readonly IWebSocketManager _webSocketManager;
    private readonly IRealtimeDataAggregator _dataAggregator;
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

    // Heartbeat 타이머 (30초마다)
    private Timer? _heartbeatTimer;

    public TradingWorker(
        ITop300StockService stockService,
        IWebSocketManager webSocketManager,
        IRealtimeDataAggregator dataAggregator,
        IConditionEvaluator conditionEvaluator,
        ICandidateTracker candidateTracker,
        IOrderExecutor orderExecutor,
        BalanceApiService balanceApiService,
        IOptions<TradingSettings> tradingSettings,
        IServiceScopeFactory scopeFactory,
        ILogger<TradingWorker> logger)
    {
        _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
        _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
        _dataAggregator = dataAggregator ?? throw new ArgumentNullException(nameof(dataAggregator));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        _candidateTracker = candidateTracker ?? throw new ArgumentNullException(nameof(candidateTracker));
        _orderExecutor = orderExecutor ?? throw new ArgumentNullException(nameof(orderExecutor));
        _balanceApiService = balanceApiService ?? throw new ArgumentNullException(nameof(balanceApiService));
        _tradingSettings = tradingSettings?.Value ?? throw new ArgumentNullException(nameof(tradingSettings));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // WebSocket 데이터 수신 이벤트 연결
        _webSocketManager.RealtimeDataReceived += OnRealtimeDataReceived;
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

                    // 2. 조건 평가 및 후보 추적 (10초마다)
                    await EvaluateConditionsAsync();

                    // 3. 만료된 후보 제거
                    _candidateTracker.RemoveExpiredCandidates();

                    // 4. 주문 실행 체크 (15:40 ET)
                    await CheckAndExecuteOrdersAsync();

                    // 5. DB 상태 업데이트 (통계)
                    await UpdateWorkerMetricsAsync();

                    // 6. 대기 (10초)
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
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

        _logger.LogInformation("Top 300 stocks loaded: {Count} stocks", top300.Count);

        // WebSocket 시작
        await _webSocketManager.StartAllSessionsAsync(top300);

        _logger.LogInformation("WebSocket sessions started: {Count} stocks subscribed",
            _webSocketManager.TotalSubscribedStockCount);

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

        // 확정된 후보 조회
        var confirmedCandidates = _candidateTracker.GetConfirmedCandidates();

        if (confirmedCandidates.Count == 0)
        {
            _logger.LogWarning("No confirmed candidates for order execution");
            _ordersExecutedToday = true;
            _lastOrderExecutionDate = DateTime.UtcNow;
            return;
        }

        // Top 2 하락 종목 선정
        var top2 = confirmedCandidates
            .OrderBy(c => c.CurrentChangeRate)
            .Take(_tradingSettings.MaxStocksToTrade)
            .ToList();

        _logger.LogInformation("Selected Top {Count} declining stocks for order: {Stocks}",
            top2.Count,
            string.Join(", ", top2.Select(s => s.ToString())));

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

        // 주문 계획 생성
        var orderPlans = new List<OrderExecutionPlan>();
        foreach (var candidate in top2)
        {
            var plan = _orderExecutor.CreateOrderPlan(
                candidate,
                accountBalance,
                (decimal)_tradingSettings.AllocationPercent / 100m); // 5% → 0.05

            orderPlans.Add(plan);
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

    public override void Dispose()
    {
        _heartbeatTimer?.Dispose();
        _webSocketManager.RealtimeDataReceived -= OnRealtimeDataReceived;
        base.Dispose();
    }
}
