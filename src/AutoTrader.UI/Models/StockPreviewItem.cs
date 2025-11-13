namespace AutoTrader.UI.Models;

/// <summary>
/// 조건 충족 종목 미리보기 아이템
/// </summary>
public class StockPreviewItem
{
    public int Rank { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public double ChangePercent { get; set; }
    public decimal TradeVolume { get; set; }
}
