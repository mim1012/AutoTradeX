namespace AutoTrader.Core.Models.Trading;

/// <summary>
/// 주문 실행 결과
/// </summary>
public class OrderResult
{
    /// <summary>
    /// 주문 성공 여부
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 주문 번호 (성공 시)
    /// </summary>
    public string? OrderNumber { get; set; }

    /// <summary>
    /// 주문 시각
    /// </summary>
    public DateTime OrderTime { get; set; }

    /// <summary>
    /// 종목 심볼
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 주문 수량
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 주문 가격 (LOC)
    /// </summary>
    public decimal OrderPrice { get; set; }

    /// <summary>
    /// 예상 총액 (Quantity × OrderPrice)
    /// </summary>
    public decimal EstimatedTotal => Quantity * OrderPrice;

    /// <summary>
    /// 오류 메시지 (실패 시)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 응답 코드
    /// </summary>
    public string? ResponseCode { get; set; }

    /// <summary>
    /// 응답 메시지
    /// </summary>
    public string? ResponseMessage { get; set; }

    public override string ToString()
    {
        if (IsSuccess)
        {
            return $"[SUCCESS] {Symbol} x{Quantity} @ ${OrderPrice} (Order#{OrderNumber})";
        }
        else
        {
            return $"[FAILED] {Symbol} x{Quantity} @ ${OrderPrice} - {ErrorMessage}";
        }
    }
}
