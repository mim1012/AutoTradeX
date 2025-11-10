namespace AutoTrader.Core.Models.Trading;

/// <summary>
/// 복합 거래 조건 (최대 3개 조건 조합)
/// </summary>
public class CompositeCondition
{
    /// <summary>
    /// 조건 목록 (최대 3개)
    /// </summary>
    public List<TradingCondition> Conditions { get; set; } = new();

    /// <summary>
    /// 조건 결합 로직 (AND/OR)
    /// </summary>
    public ConditionLogic Logic { get; set; } = ConditionLogic.And;

    /// <summary>
    /// 복합 조건 이름
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 조건 추가 (최대 3개)
    /// </summary>
    public void AddCondition(TradingCondition condition)
    {
        if (Conditions.Count >= 3)
        {
            throw new InvalidOperationException("Cannot add more than 3 conditions");
        }

        Conditions.Add(condition);
    }

    /// <summary>
    /// 활성화된 조건 수
    /// </summary>
    public int EnabledConditionCount => Conditions.Count(c => c.IsEnabled);

    /// <summary>
    /// 복합 조건을 문자열로 표현
    /// 예: "등락률 < -3.0% AND 거래대금 > 1000000"
    /// </summary>
    public override string ToString()
    {
        var enabledConditions = Conditions.Where(c => c.IsEnabled).ToList();

        if (enabledConditions.Count == 0)
            return "조건 없음";

        var logicStr = Logic == ConditionLogic.And ? " AND " : " OR ";
        return string.Join(logicStr, enabledConditions.Select(c => c.ToString()));
    }
}
