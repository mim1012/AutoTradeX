namespace AutoTrader.Core.Models.Trading;

/// <summary>
/// 주문 실행 계획
/// </summary>
public class OrderExecutionPlan
{
    /// <summary>
    /// 대상 종목 정보
    /// </summary>
    public CandidateStock Candidate { get; set; } = null!;

    /// <summary>
    /// 주문 수량
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// LOC 주문 가격 (현재가 × 1.05)
    /// </summary>
    public decimal LocPrice { get; set; }

    /// <summary>
    /// 예상 투자금액
    /// </summary>
    public decimal EstimatedAmount => Quantity * LocPrice;

    /// <summary>
    /// 계획 생성 시각
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 사용 가능한 투자 금액 (계좌 잔고 × 할당 비율)
    /// </summary>
    public decimal AvailableAmount { get; set; }

    /// <summary>
    /// 할당 비율 (5% = 0.05)
    /// </summary>
    public decimal AllocationPercent { get; set; } = 0.05m;

    public override string ToString()
    {
        return $"{Candidate.Symbol} x{Quantity} @ ${LocPrice} (Total: ${EstimatedAmount})";
    }
}
