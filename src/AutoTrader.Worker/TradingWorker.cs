using AutoTrader.Core.Configuration;
using AutoTrader.Core.Models.Trading;
using AutoTrader.Core.Models.WebSocket;
using AutoTrader.Core.Services.Realtime;
using AutoTrader.Core.Services.Stock;
using AutoTrader.Core.Services.Trading;
using AutoTrader.Core.Services.WebSocket;
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
    private readonly TradingSettings _tradingSettings;
    private readonly ILogger<TradingWorker> _logger;

    // 거래 조건 (설정 기반으로 구성)
    private CompositeCondition _tradingCondition = null!;

    // 주문 실행 완료 플래그
    private bool _ordersExecutedToday = false;
    private DateTime _lastOrderExecutionDate = DateTime.MinValue;

    public TradingWorker(
        ITop300StockService stockService,
        IWebSocketManager webSocketManager,
        IRealtimeDataAggregator dataAggregator,
        IConditionEvaluator conditionEvaluator,
        ICandidateTracker candidateTracker,
        IOrderExecutor orderExecutor,
        IOptions<TradingSettings> tradingSettings,
        ILogger<TradingWorker> logger)
    {
        _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
        _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
        _dataAggregator = dataAggregator ?? throw new ArgumentNullException(nameof(dataAggregator));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        _candidateTracker = candidateTracker ?? throw new ArgumentNullException(nameof(candidateTracker));
        _orderExecutor = orderExecutor ?? throw new ArgumentNullException(nameof(orderExecutor));
        _tradingSettings = tradingSettings?.Value ?? throw new ArgumentNullException(nameof(tradingSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // WebSocket 데이터 수신 이벤트 연결
        _webSocketManager.RealtimeDataReceived += OnRealtimeDataReceived;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TradingWorker started");

        try
        {
            // 초기화
            await InitializeAsync(stoppingToken);

            // 메인 루프
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. 조건 평가 및 후보 추적 (10초마다)
                    await EvaluateConditionsAsync();

                    // 2. 만료된 후보 제거
                    _candidateTracker.RemoveExpiredCandidates();

                    // 3. 주문 실행 체크 (15:50 ET)
                    await CheckAndExecuteOrdersAsync();

                    // 4. 대기 (10초)
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in main loop");
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
            throw;
        }
        finally
        {
            await CleanupAsync();
        }
    }

    /// <summary>
    /// 초기화
    /// </summary>
    private async Task InitializeAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Initializing TradingWorker...");

        // 거래 조건 구성 (예시: 등락률 < -3%)
        _tradingCondition = new CompositeCondition
        {
            Name = "하락률 3% 이상",
            Logic = ConditionLogic.And,
            Conditions = new List<TradingCondition>
            {
                new TradingCondition
                {
                    Name = "등락률 하락",
                    Type = ConditionType.ChangeRate,
                    Operator = ConditionOperator.LessThan,
                    Value = -3.0m, // -3% 이하
                    IsEnabled = true,
                    Description = "등락률이 -3% 이하인 종목"
                }
            }
        };

        _logger.LogInformation("Trading condition configured: {Condition}", _tradingCondition);

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

        // TODO: 실제 계좌 잔고 조회 (현재는 더미)
        var accountBalance = 10000m; // $10,000 (더미)

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

    public override void Dispose()
    {
        _webSocketManager.RealtimeDataReceived -= OnRealtimeDataReceived;
        base.Dispose();
    }
}
