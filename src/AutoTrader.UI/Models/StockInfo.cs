namespace AutoTrader.UI.Models;

/// <summary>
/// UI에 표시할 종목 정보
/// </summary>
public class StockInfo
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal ChangeRate { get; set; }
    public decimal TradeAmount { get; set; }
    public int Rank { get; set; }
    public bool IsConditionMet { get; set; }
    public bool IsCandidate { get; set; }
}
