using AutoTrader.Core.Configuration;
using AutoTrader.Core.Models.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoTrader.Core.Services.Trading;

/// <summary>
/// 주문 실행 서비스 구현
/// - LOC 주문 실행 (현재가 × 1.05)
/// - 마감 10분 전 주문 시간 체크
/// - 수량 계산 (계좌 잔고 × 할당 비율)
/// </summary>
public class OrderExecutor : IOrderExecutor
{
    private readonly OrderApiService _orderApi;
    private readonly TradingSettings _tradingSettings;
    private readonly ILogger<OrderExecutor> _logger;

    // 미국 동부 시간 (ET) 마감 10분 전 시각
    private static readonly TimeSpan OrderStartTime = new(15, 50, 0); // 15:50 ET
    private static readonly TimeSpan OrderEndTime = new(16, 0, 0);    // 16:00 ET (마감)

    public OrderExecutor(
        OrderApiService orderApi,
        IOptions<TradingSettings> tradingSettings,
        ILogger<OrderExecutor> logger)
    {
        _orderApi = orderApi ?? throw new ArgumentNullException(nameof(orderApi));
        _tradingSettings = tradingSettings?.Value ?? throw new ArgumentNullException(nameof(tradingSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public OrderExecutionPlan CreateOrderPlan(
        CandidateStock candidate,
        decimal accountBalance,
        decimal allocationPercent = 0.05m)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));

        if (accountBalance <= 0)
            throw new ArgumentException("Account balance must be positive", nameof(accountBalance));

        // 할당 금액 계산 (예: $10,000 × 5% = $500)
        var availableAmount = accountBalance * allocationPercent;

        // LOC 가격 계산 (현재가 × 1.05)
        var locPrice = candidate.CurrentPrice * (decimal)_tradingSettings.LimitPriceMultiplier;

        // 주문 수량 계산 (소수점 버림)
        var quantity = (int)(availableAmount / locPrice);

        if (quantity <= 0)
        {
            _logger.LogWarning("Calculated quantity is 0 for {Symbol} (Balance: ${Balance}, Price: ${Price})",
                candidate.Symbol, accountBalance, locPrice);
            quantity = 1; // 최소 1주
        }

        var plan = new OrderExecutionPlan
        {
            Candidate = candidate,
            Quantity = quantity,
            LocPrice = locPrice,
            AvailableAmount = availableAmount,
            AllocationPercent = allocationPercent
        };

        _logger.LogInformation("Order plan created: {Plan}", plan);

        return plan;
    }

    /// <inheritdoc/>
    public async Task<OrderResult> ExecuteOrderAsync(OrderExecutionPlan plan)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        _logger.LogInformation("Executing order: {Symbol} x{Quantity} @ ${Price}",
            plan.Candidate.Symbol, plan.Quantity, plan.LocPrice);

        var result = new OrderResult
        {
            Symbol = plan.Candidate.Symbol,
            Quantity = plan.Quantity,
            OrderPrice = plan.LocPrice,
            OrderTime = DateTime.UtcNow
        };

        try
        {
            // 주문 시간 체크
            if (!IsOrderTimeWindow())
            {
                var secondsUntil = SecondsUntilOrderTime();
                result.IsSuccess = false;
                result.ErrorMessage = secondsUntil > 0
                    ? $"Not in order time window (starts in {secondsUntil}s)"
                    : "Order time window has passed";

                _logger.LogWarning("Order rejected: {Message}", result.ErrorMessage);
                return result;
            }

            // KIS API 주문 실행
            var orderResponse = await _orderApi.PlaceLocBuyOrderAsync(
                plan.Candidate.Symbol,
                plan.Quantity,
                plan.LocPrice);

            // 응답 매핑
            result.IsSuccess = orderResponse.IsSuccess;
            result.ResponseCode = orderResponse.ResponseCode;
            result.ResponseMessage = orderResponse.Message;

            if (orderResponse.IsSuccess && orderResponse.Output != null)
            {
                result.OrderNumber = orderResponse.Output.OrderNumber;
                _logger.LogInformation("Order executed successfully: {Result}", result);
            }
            else
            {
                result.ErrorMessage = orderResponse.Message;
                _logger.LogWarning("Order execution failed: {Result}", result);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Exception during order execution for {Symbol}", plan.Candidate.Symbol);

            return result;
        }
    }

    /// <inheritdoc/>
    public async Task<List<OrderResult>> ExecuteOrdersAsync(List<OrderExecutionPlan> plans)
    {
        if (plans == null || plans.Count == 0)
            return new List<OrderResult>();

        _logger.LogInformation("Executing {Count} orders", plans.Count);

        var results = new List<OrderResult>();

        // 순차적으로 주문 실행 (동시 주문 방지)
        foreach (var plan in plans)
        {
            var result = await ExecuteOrderAsync(plan);
            results.Add(result);

            // 주문 간격 (API 부하 방지)
            await Task.Delay(1000);
        }

        var successCount = results.Count(r => r.IsSuccess);
        _logger.LogInformation("Order execution complete: {Success}/{Total} succeeded",
            successCount, results.Count);

        return results;
    }

    /// <inheritdoc/>
    public bool IsOrderTimeWindow()
    {
        // 현재 ET 시간 계산 (UTC - 5시간, DST 고려 필요)
        var etNow = GetEasternTime();
        var currentTime = etNow.TimeOfDay;

        // 15:50 ~ 16:00 ET 범위 체크
        return currentTime >= OrderStartTime && currentTime < OrderEndTime;
    }

    /// <inheritdoc/>
    public int SecondsUntilOrderTime()
    {
        var etNow = GetEasternTime();
        var currentTime = etNow.TimeOfDay;

        // 이미 주문 시간이 지났으면 음수 반환
        if (currentTime >= OrderEndTime)
        {
            return -(int)(currentTime - OrderEndTime).TotalSeconds;
        }

        // 아직 주문 시간 전이면 양수 반환
        if (currentTime < OrderStartTime)
        {
            return (int)(OrderStartTime - currentTime).TotalSeconds;
        }

        // 현재 주문 시간 내면 0 반환
        return 0;
    }

    /// <summary>
    /// 미국 동부 시간 (ET) 계산
    /// DST (Daylight Saving Time) 자동 처리
    /// </summary>
    private static DateTime GetEasternTime()
    {
        try
        {
            // .NET TimeZoneInfo를 사용하여 DST 자동 처리
            var etZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, etZone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Linux 환경에서는 "America/New_York" 사용
            var etZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, etZone);
        }
    }
}
