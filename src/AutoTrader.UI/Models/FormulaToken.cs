namespace AutoTrader.UI.Models;

/// <summary>
/// 조건식 토큰 타입
/// </summary>
public enum FormulaTokenType
{
    ConditionId,    // 조건 ID (A, B, C, ...)
    Operator,       // 논리 연산자 (and, or)
    OpenParen,      // 여는 괄호 (
    CloseParen      // 닫는 괄호 )
}

/// <summary>
/// 조건식 토큰
/// </summary>
public class FormulaToken
{
    public FormulaTokenType Type { get; set; }
    public string Value { get; set; } = string.Empty;

    public FormulaToken(FormulaTokenType type, string value)
    {
        Type = type;
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
