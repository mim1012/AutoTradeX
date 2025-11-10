using AutoTrader.Core.Models.Trading;

namespace AutoTrader.Core.Services.Trading;

/// <summary>
/// 주문 실행 서비스 인터페이스
/// - LOC (Limit on Close) 주문 실행
/// - 마감 10분 전 주문 (15:50 ET)
/// - 현재가 × 1.05 가격 설정
/// </summary>
public interface IOrderExecutor
{
    /// <summary>
    /// 주문 실행 계획 생성
    /// </summary>
    /// <param name="candidate">매수 후보 종목</param>
    /// <param name="accountBalance">계좌 잔고</param>
    /// <param name="allocationPercent">할당 비율 (기본값: 5%)</param>
    /// <returns>주문 실행 계획</returns>
    OrderExecutionPlan CreateOrderPlan(
        CandidateStock candidate,
        decimal accountBalance,
        decimal allocationPercent = 0.05m);

    /// <summary>
    /// LOC 주문 실행
    /// </summary>
    /// <param name="plan">주문 실행 계획</param>
    /// <returns>주문 결과</returns>
    Task<OrderResult> ExecuteOrderAsync(OrderExecutionPlan plan);

    /// <summary>
    /// 여러 종목에 대한 주문 실행 (순차적)
    /// </summary>
    /// <param name="plans">주문 계획 목록</param>
    /// <returns>주문 결과 목록</returns>
    Task<List<OrderResult>> ExecuteOrdersAsync(List<OrderExecutionPlan> plans);

    /// <summary>
    /// 주문 실행 가능 시간 체크 (15:50 ET ~ 16:00 ET)
    /// </summary>
    /// <returns>주문 가능 여부</returns>
    bool IsOrderTimeWindow();

    /// <summary>
    /// 마감 10분 전까지 남은 시간 (초)
    /// </summary>
    /// <returns>남은 시간 (음수면 주문 시간 지남)</returns>
    int SecondsUntilOrderTime();
}
