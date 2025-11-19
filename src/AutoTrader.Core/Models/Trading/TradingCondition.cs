using AutoTrader.Core.Models.Market;

namespace AutoTrader.Core.Models.Trading;

/// <summary>
/// 거래 조건 정의
/// </summary>
public class TradingCondition
{
    /// <summary>
    /// 조건 ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 조건 이름
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 조건 타입
    /// </summary>
    public ConditionType Type { get; set; }

    /// <summary>
    /// 비교 연산자
    /// </summary>
    public ConditionOperator Operator { get; set; }

    /// <summary>
    /// 비교 값 (예: 가격 100, 등락률 5.0%)
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// 조건 활성화 여부
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 조건 설명
    /// </summary>
    public string Description { get; set; } = string.Empty;

    // ===== MovingAverage 조건 전용 파라미터 =====

    /// <summary>
    /// 이동평균선 타입 (10, 20, 120일)
    /// </summary>
    public MovingAverageType? MaType { get; set; }

    /// <summary>
    /// 이동평균선 조건의 최소 범위 (%)
    /// </summary>
    public decimal? MaRangeMin { get; set; }

    /// <summary>
    /// 이동평균선 조건의 최대 범위 (%)
    /// </summary>
    public decimal? MaRangeMax { get; set; }

    // ===== PriceComparison 조건 전용 파라미터 =====

    /// <summary>
    /// 캔들 타입 (일봉/주봉)
    /// </summary>
    public CandleType? CandleType { get; set; }

    /// <summary>
    /// 캔들 오프셋 A (0=최신봉, 1=1봉 전, ...)
    /// </summary>
    public int CandleOffsetA { get; set; }

    /// <summary>
    /// 가격 요소 A (시가/고가/저가/종가)
    /// </summary>
    public PriceElement? PriceElementA { get; set; }

    /// <summary>
    /// 캔들 오프셋 B
    /// </summary>
    public int CandleOffsetB { get; set; }

    /// <summary>
    /// 가격 요소 B
    /// </summary>
    public PriceElement? PriceElementB { get; set; }

    /// <summary>
    /// 조건을 문자열로 표현
    /// 예: "등락률 < -3.0%"
    /// </summary>
    public override string ToString()
    {
        var operatorSymbol = Operator switch
        {
            ConditionOperator.GreaterThan => ">",
            ConditionOperator.LessThan => "<",
            ConditionOperator.Equals => "=",
            ConditionOperator.GreaterThanOrEquals => ">=",
            ConditionOperator.LessThanOrEquals => "<=",
            _ => "?"
        };

        var valueStr = Type == ConditionType.ChangeRate
            ? $"{Value}%"
            : Value.ToString();

        var typeName = Type switch
        {
            ConditionType.Price => "현재가",
            ConditionType.ChangeRate => "등락률",
            ConditionType.Volume => "거래량",
            ConditionType.TradeAmount => "거래대금",
            ConditionType.MovingAverage => "이동평균선",
            ConditionType.PriceComparison => "주가비교",
            _ => "조건"
        };

        // MovingAverage와 PriceComparison은 Description 사용
        if (Type == ConditionType.MovingAverage || Type == ConditionType.PriceComparison)
        {
            return Description;
        }

        return $"{typeName} {operatorSymbol} {valueStr}";
    }
}
