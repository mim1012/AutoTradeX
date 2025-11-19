using AutoTrader.Core.Configuration;
using AutoTrader.Core.Models.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace AutoTrader.Core.Services.Trading;

/// <summary>
/// 주문 실행 서비스 구현
/// - LOC 주문 실행 (현재가 × 1.05)
/// - 마감 10분 전 주문 시간 체크
/// - 수량 계산 (계좌 잔고 × 할당 비율)
/// - Polly 재시도 정책 (네트워크 오류 시 자동 재시도)
/// </summary>
public class OrderExecutor : IOrderExecutor
{
    private readonly OrderApiService _orderApi;
    private readonly TradingSettings _tradingSettings;
    private readonly ILogger<OrderExecutor> _logger;

    // 미국 동부 시간 (ET) 주문 시간 (마감 20분 전부터)
    private static readonly TimeSpan OrderStartTime = new(15, 40, 0); // 15:40 ET (마감 20분 전)
    private static readonly TimeSpan OrderEndTime = new(16, 0, 0);    // 16:00 ET (마감)

    // Polly 재시도 정책 (네트워크 오류 시 3회 재시도, 지수 백오프)
    private readonly AsyncRetryPolicy _retryPolicy;

    public OrderExecutor(
        OrderApiService orderApi,
        IOptions<TradingSettings> tradingSettings,
        ILogger<OrderExecutor> logger)
    {
        _orderApi = orderApi ?? throw new ArgumentNullException(nameof(orderApi));
        _tradingSettings = tradingSettings?.Value ?? throw new ArgumentNullException(nameof(tradingSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Polly 재시도 정책 설정 (3회 재시도, 지수 백오프: 1s, 2s, 4s)
        _retryPolicy = Policy
            .Handle<HttpRequestException>() // 네트워크 오류
            .Or<TaskCanceledException>()     // 타임아웃
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "Order execution failed (attempt {RetryCount}/3), retrying in {Delay}s...",
                        retryCount, timeSpan.TotalSeconds);
                });

        _logger.LogInformation("OrderExecutor initialized with Polly retry policy (3 attempts, exponential backoff)");
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

        // 할당 금액 계산 (예: $10,000 × 50% = $5,000)
        var availableAmount = accountBalance * allocationPercent;

        // LOC 가격 계산 (현재가 × LimitPriceMultiplier, 기본 1.03 = +3%)
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

            // KIS API 주문 실행 (Polly 재시도 정책 적용)
            var orderResponse = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _orderApi.PlaceLocBuyOrderAsync(
                    plan.Candidate.Symbol,
                    plan.Quantity,
                    plan.LocPrice);
            });

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

        // 15:40 ~ 16:00 ET 범위 체크 (마감 20분 전부터)
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
    private DateTime GetEasternTime()
    {
        try
        {
            // .NET TimeZoneInfo를 사용하여 DST 자동 처리
            // EST (UTC-5): 11월~3월 / EDT (UTC-4): 3월~11월
            var etZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, etZone);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Linux 환경에서는 "America/New_York" 사용
                var etZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, etZone);
            }
            catch (TimeZoneNotFoundException ex)
            {
                // Fallback: Manual UTC offset calculation with DST estimation
                _logger.LogError(ex, "Failed to find ET timezone, using manual UTC offset with DST estimation");

                var now = DateTime.UtcNow;
                var isDst = IsDaylightSavingTimePeriod(now);
                var offset = isDst ? -4 : -5;

                return now.AddHours(offset);
            }
        }
    }

    /// <summary>
    /// DST(서머타임) 기간 추정
    /// 3월 두 번째 일요일 2AM ~ 11월 첫 번째 일요일 2AM
    /// </summary>
    private static bool IsDaylightSavingTimePeriod(DateTime utcNow)
    {
        var year = utcNow.Year;

        // DST 시작: 3월 두 번째 일요일 2AM EST (= 7AM UTC)
        var marchSecondSunday = GetNthSunday(year, 3, 2);
        var dstStart = new DateTime(year, 3, marchSecondSunday, 7, 0, 0, DateTimeKind.Utc);

        // DST 종료: 11월 첫 번째 일요일 2AM EDT (= 6AM UTC)
        var novemberFirstSunday = GetNthSunday(year, 11, 1);
        var dstEnd = new DateTime(year, 11, novemberFirstSunday, 6, 0, 0, DateTimeKind.Utc);

        return utcNow >= dstStart && utcNow < dstEnd;
    }

    /// <summary>
    /// 특정 월의 N번째 일요일 날짜 계산
    /// </summary>
    private static int GetNthSunday(int year, int month, int n)
    {
        var firstDay = new DateTime(year, month, 1);
        var firstSunday = firstDay.Day + ((7 - (int)firstDay.DayOfWeek) % 7);
        if (firstSunday == 0) firstSunday = 7;
        return firstSunday + (n - 1) * 7;
    }
}
