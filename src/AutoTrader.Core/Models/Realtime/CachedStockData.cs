using AutoTrader.Core.DTOs.WebSocket;

namespace AutoTrader.Core.Models.Realtime;

/// <summary>
/// 캐시된 실시간 주식 데이터
/// </summary>
public class CachedStockData
{
    /// <summary>
    /// 종목 심볼
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 최신 실시간 데이터
    /// </summary>
    public RealtimeStockData LatestData { get; set; } = null!;

    /// <summary>
    /// 마지막 업데이트 시각 (UTC)
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// 총 업데이트 횟수
    /// </summary>
    public long UpdateCount { get; set; }

    /// <summary>
    /// 데이터가 신선한지 여부 (10초 이내 업데이트)
    /// </summary>
    public bool IsFresh => (DateTime.UtcNow - LastUpdatedAt).TotalSeconds < 10;

    /// <summary>
    /// 현재가 (decimal)
    /// </summary>
    public decimal CurrentPrice => LatestData?.CurrentPriceDecimal ?? 0;

    /// <summary>
    /// 등락률 (decimal)
    /// </summary>
    public decimal ChangeRate => LatestData?.ChangeRateDecimal ?? 0;

    /// <summary>
    /// 거래대금 (long)
    /// </summary>
    public long TradeAmount => LatestData?.TradeAmountLong ?? 0;
}
