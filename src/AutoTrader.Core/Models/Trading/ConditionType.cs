namespace AutoTrader.Core.Models.Trading;

/// <summary>
/// 거래 조건 타입
/// </summary>
public enum ConditionType
{
    /// <summary>현재가 조건 (Price > X, Price < X)</summary>
    Price = 1,

    /// <summary>등락률 조건 (ChangeRate > X%, ChangeRate < X%)</summary>
    ChangeRate = 2,

    /// <summary>거래량 조건 (Volume > X)</summary>
    Volume = 3,

    /// <summary>거래대금 조건 (TradeAmount > X)</summary>
    TradeAmount = 4,

    /// <summary>이동평균선 조건 (현재가가 MA 대비 ±X% 이내)</summary>
    MovingAverage = 5,

    /// <summary>주가 비교 조건 (n봉 OHLC 요소 간 비교)</summary>
    PriceComparison = 6,

    /// <summary>복합 조건 (여러 조건 조합)</summary>
    Combined = 99
}

/// <summary>
/// 조건 비교 연산자
/// </summary>
public enum ConditionOperator
{
    /// <summary>보다 큼 (>)</summary>
    GreaterThan = 1,

    /// <summary>보다 작음 (<)</summary>
    LessThan = 2,

    /// <summary>같음 (=)</summary>
    Equals = 3,

    /// <summary>이상 (>=)</summary>
    GreaterThanOrEquals = 4,

    /// <summary>이하 (<=)</summary>
    LessThanOrEquals = 5
}

/// <summary>
/// 조건 결합 연산자
/// </summary>
public enum ConditionLogic
{
    /// <summary>논리곱 (모든 조건 만족)</summary>
    And = 1,

    /// <summary>논리합 (하나 이상 조건 만족)</summary>
    Or = 2
}
