namespace AutoTrader.Core.Models.Trading;

/// <summary>
/// 매수 후보 종목
/// </summary>
public class CandidateStock
{
    /// <summary>
    /// 종목 심볼
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 종목명
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 첫 번째 조건 만족 시각
    /// </summary>
    public DateTime FirstConfirmedAt { get; set; }

    /// <summary>
    /// 두 번째 조건 만족 시각 (null이면 1회만 확인됨)
    /// </summary>
    public DateTime? SecondConfirmedAt { get; set; }

    /// <summary>
    /// 현재 등락률 (%)
    /// </summary>
    public decimal CurrentChangeRate { get; set; }

    /// <summary>
    /// 현재가
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// 거래대금
    /// </summary>
    public long TradeAmount { get; set; }

    /// <summary>
    /// 2회 연속 확인 완료 여부
    /// </summary>
    public bool IsFullyConfirmed => SecondConfirmedAt.HasValue;

    /// <summary>
    /// 첫 확인 이후 경과 시간 (초)
    /// </summary>
    public double SecondsSinceFirstConfirmation =>
        (DateTime.UtcNow - FirstConfirmedAt).TotalSeconds;

    /// <summary>
    /// 두 번째 확인까지 대기 시간 초과 여부 (20초 초과)
    /// </summary>
    public bool IsExpired => SecondsSinceFirstConfirmation > 20;

    public override string ToString()
    {
        var status = IsFullyConfirmed ? "확정" : "1차 확인";
        return $"{Symbol} ({Name}) [{status}] {CurrentChangeRate}%";
    }
}
