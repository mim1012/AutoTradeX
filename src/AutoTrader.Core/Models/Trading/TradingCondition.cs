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
            _ => "조건"
        };

        return $"{typeName} {operatorSymbol} {valueStr}";
    }
}
