namespace AutoTrader.UI.Models;

/// <summary>
/// 조건 타입
/// </summary>
public enum ConditionType
{
    /// <summary>
    /// A) 등락률 조건
    /// </summary>
    PriceChange,
    
    /// <summary>
    /// B) 이동평균선 조건
    /// </summary>
    MovingAverage,
    
    /// <summary>
    /// C) 거래대금 조건
    /// </summary>
    TradeVolume,
    
    /// <summary>
    /// D) 주가 비교 조건
    /// </summary>
    PriceComparison
}
