using AutoTrader.Core.Models.Trading;
using AutoTrader.Core.Services.Realtime;
using AutoTrader.Core.Services.Stock;
using AutoTrader.Core.Services.Trading;
using AutoTrader.Core.Services.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTrader.Worker;

/// <summary>
/// 자동매매 시스템 메인 오케스트레이터
/// - 시작 시: 인증, Top 300 조회, WebSocket 시작
/// - 실시간 루프: 조건 평가, 후보 추적
/// - 마감 10분 전: 최종 후보 선정 및 주문 실행
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ITop300StockService _top300Service;
    private readonly IWebSocketManager _webSocketManager;
    private readonly IRealtimeDataAggregator _dataAggregator;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly ICandidateTracker _candidateTracker;
    private readonly IOrderExecutor _orderExecutor;

    // 조건식 (임시로 하드코딩, 추후 UI에서 설정)
    private CompositeCondition? _tradingCondition;

    // 주문 실행 완료 플래그
    private bool _orderExecutedToday = false;
    private DateTime _lastOrderDate = DateTime.MinValue;

    public Worker(
        ILogger<Worker> logger,
        ITop300StockService top300Service,
        IWebSocketManager webSocketManager,
        IRealtimeDataAggregator dataAggregator,
        IConditionEvaluator conditionEvaluator,
        ICandidateTracker candidateTracker,
        IOrderExecutor orderExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _top300Service = top300Service ?? throw new ArgumentNullException(nameof(top300Service));
        _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
        _dataAggregator = dataAggregator ?? throw new ArgumentNullException(nameof(dataAggregator));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        _candidateTracker = candidateTracker ?? throw new ArgumentNullException(nameof(candidateTracker));
        _orderExecutor = orderExecutor ?? throw new ArgumentNullException(nameof(orderExecutor));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=== AutoTrader Worker Service Starting ===");

        try
        {
            // 1. 초기화
            await InitializeAsync(stoppingToken);

            // 2. 메인 루프
            await RunMainLoopAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in Worker Service");
            throw;
        }
        finally
        {
            // 3. 정리
            await CleanupAsync();
        }
    }

    /// <summary>
    /// 시스템 초기화
    /// </summary>
    private async Task InitializeAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Initializing AutoTrader system...");

        // 1. Top 300 종목 최초 조회
        _logger.LogInformation("Fetching initial Top 300 stocks...");
        var top300Stocks = await _top300Service.GetTop300StocksAsync();
        _logger.LogInformation("Loaded {Count} stocks", top300Stocks.Count);

        // 2. WebSocket 세션 시작
        _logger.LogInformation("Starting WebSocket sessions...");
        await _webSocketManager.StartAllSessionsAsync(top300Stocks);

        // WebSocket 데이터 수신 이벤트 연결
        _webSocketManager.RealtimeDataReceived += OnRealtimeDataReceived;

        // 3. 조건식 설정 (임시 - 하락률 -5% 이하)
        _tradingCondition = new CompositeCondition
        {
            Logic = ConditionLogic.And,
            Conditions = new List<TradingCondition>
            {
                new TradingCondition
                {
                    Type = ConditionType.ChangeRate,
                    Operator = ConditionOperator.LessThanOrEquals,
                    Value = -5.0m,
                    IsEnabled = true
                }
            }
        };

        _logger.LogInformation("Initialization complete. Trading condition: {Condition}", _tradingCondition);
    }

    /// <summary>
    /// 메인 실행 루프
    /// </summary>
    private async Task RunMainLoopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Entering main trading loop...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 날짜가 바뀌면 주문 플래그 리셋
                ResetDailyOrderFlagIfNeeded();

                // 10초마다 조건 평가 및 후보 추적
                await EvaluateConditionsAndTrackCandidatesAsync();

                // 만료된 후보 제거
                _candidateTracker.RemoveExpiredCandidates();

                // 마감 10분 전이면 주문 실행
                if (_orderExecutor.IsOrderTimeWindow() && !_orderExecutedToday)
                {
                    await ExecuteOrdersAsync();
                }

                // 상태 로깅
                LogCurrentStatus();

                // 10초 대기
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in main loop iteration");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Main trading loop exited");
    }

    /// <summary>
    /// 조건 평가 및 후보 추적
    /// </summary>
    private async Task EvaluateConditionsAndTrackCandidatesAsync()
    {
        if (_tradingCondition == null)
        {
            _logger.LogWarning("Trading condition is not set");
            return;
        }

        // 실시간 데이터 가져오기
        var allStockData = _dataAggregator.GetAllStockData();

        if (allStockData.Count == 0)
        {
            _logger.LogDebug("No realtime data available yet");
            return;
        }

        // 조건 평가 (병렬 처리)
        var matchedStocks = _conditionEvaluator.EvaluateAllStocks(_tradingCondition, allStockData);

        if (matchedStocks.Count > 0)
        {
            _logger.LogInformation("Found {Count} stocks matching conditions", matchedStocks.Count);

            // 후보 추적 (2회 확인 로직)
            _candidateTracker.TrackCandidates(matchedStocks);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 주문 실행
    /// </summary>
    private async Task ExecuteOrdersAsync()
    {
        _logger.LogInformation("=== Order Execution Window ===");

        // 최종 확정된 후보 가져오기 (2회 확인 완료)
        var confirmedCandidates = _candidateTracker.GetConfirmedCandidates();

        if (confirmedCandidates.Count == 0)
        {
            _logger.LogInformation("No confirmed candidates for order execution");
            _orderExecutedToday = true; // 오늘은 더 이상 주문 시도 안 함
            return;
        }

        _logger.LogInformation("Confirmed candidates: {Count}", confirmedCandidates.Count);

        // 하락률 상위 2종목 선정 (이미 정렬되어 있음)
        var topCandidates = confirmedCandidates.Take(2).ToList();

        _logger.LogInformation("Selected top {Count} candidates for trading:", topCandidates.Count);
        foreach (var candidate in topCandidates)
        {
            _logger.LogInformation("  - {Symbol}: {ChangeRate:F2}% (Price: ${Price})",
                candidate.Symbol, candidate.CurrentChangeRate, candidate.CurrentPrice);
        }

        // TODO: 계좌 잔고 조회 (현재는 임시로 $10,000 가정)
        decimal accountBalance = 10000m;

        // 자금 배분 (1개면 100%, 2개면 각 50%)
        decimal allocationPercent = topCandidates.Count == 1 ? 1.0m : 0.5m;

        // 주문 계획 생성
        var orderPlans = topCandidates.Select(candidate =>
            _orderExecutor.CreateOrderPlan(candidate, accountBalance, allocationPercent)
        ).ToList();

        // 주문 실행
        var orderResults = await _orderExecutor.ExecuteOrdersAsync(orderPlans);

        // 결과 로깅
        foreach (var result in orderResults)
        {
            if (result.IsSuccess)
            {
                _logger.LogInformation("✅ Order SUCCESS: {Symbol} x{Quantity} @ ${Price}, OrderNo: {OrderNo}",
                    result.Symbol, result.Quantity, result.OrderPrice, result.OrderNumber);
            }
            else
            {
                _logger.LogWarning("❌ Order FAILED: {Symbol}, Reason: {Reason}",
                    result.Symbol, result.ErrorMessage);
            }
        }

        // 주문 완료 플래그 설정
        _orderExecutedToday = true;
        _lastOrderDate = DateTime.UtcNow.Date;

        _logger.LogInformation("=== Order Execution Complete ===");
    }

    /// <summary>
    /// WebSocket 실시간 데이터 수신 이벤트 핸들러
    /// </summary>
    private void OnRealtimeDataReceived(object? sender, AutoTrader.Core.Models.WebSocket.RealtimeDataReceivedEventArgs e)
    {
        // 데이터 집계기에 저장 (Thread-safe)
        _dataAggregator.UpdateStockData(e.Data);
    }

    /// <summary>
    /// 날짜가 바뀌면 주문 플래그 리셋
    /// </summary>
    private void ResetDailyOrderFlagIfNeeded()
    {
        var today = DateTime.UtcNow.Date;
        if (_lastOrderDate < today)
        {
            _logger.LogInformation("New trading day started. Resetting order flag.");
            _orderExecutedToday = false;
            _candidateTracker.ClearCandidates();
        }
    }

    /// <summary>
    /// 현재 상태 로깅
    /// </summary>
    private void LogCurrentStatus()
    {
        var secondsUntilOrder = _orderExecutor.SecondsUntilOrderTime();
        var confirmedCount = _candidateTracker.ConfirmedCandidateCount;
        var pendingCount = _candidateTracker.PendingCandidateCount;

        if (secondsUntilOrder > 0 && secondsUntilOrder <= 600) // 10분 이내
        {
            _logger.LogInformation(
                "[Status] Order window in {Seconds}s | Confirmed: {Confirmed} | Pending: {Pending}",
                secondsUntilOrder, confirmedCount, pendingCount);
        }
        else if (confirmedCount > 0 || pendingCount > 0)
        {
            _logger.LogDebug(
                "[Status] Confirmed: {Confirmed} | Pending: {Pending}",
                confirmedCount, pendingCount);
        }
    }

    /// <summary>
    /// 정리 작업
    /// </summary>
    private async Task CleanupAsync()
    {
        _logger.LogInformation("Cleaning up AutoTrader system...");

        try
        {
            // WebSocket 이벤트 해제
            _webSocketManager.RealtimeDataReceived -= OnRealtimeDataReceived;

            // WebSocket 세션 종료
            await _webSocketManager.StopAllSessionsAsync();

            _logger.LogInformation("Cleanup complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }
}
