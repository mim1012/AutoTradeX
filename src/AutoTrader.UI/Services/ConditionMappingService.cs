using AutoTrader.Core.Models.Trading;
using AutoTrader.UI.Models;
using AutoTrader.UI.ViewModels;
using System.Text.RegularExpressions;

namespace AutoTrader.UI.Services;

/// <summary>
/// UI 조건과 Core 조건 간 변환 서비스
/// </summary>
public class ConditionMappingService
{
    /// <summary>
    /// UI 조건을 Core 조건으로 변환
    /// </summary>
    public TradingCondition MapToCore(ConditionItemViewModel uiCondition)
    {
        if (uiCondition == null)
            throw new ArgumentNullException(nameof(uiCondition));

        return uiCondition.Type switch
        {
            Models.ConditionType.PriceChange => MapPriceChangeCondition(uiCondition),
            Models.ConditionType.MovingAverage => MapMovingAverageCondition(uiCondition),
            Models.ConditionType.TradeVolume => MapTradeVolumeCondition(uiCondition),
            Models.ConditionType.PriceComparison => MapPriceComparisonCondition(uiCondition),
            _ => throw new NotSupportedException($"Unsupported condition type: {uiCondition.Type}")
        };
    }

    /// <summary>
    /// UI 조건 목록을 Core CompositeCondition으로 변환
    /// </summary>
    public CompositeCondition MapToCompositeCondition(
        List<ConditionItemViewModel> uiConditions,
        ConditionLogic logic)
    {
        var coreConditions = uiConditions
            .Where(c => c.IsEnabled)
            .Select(MapToCore)
            .ToList();

        return new CompositeCondition
        {
            Conditions = coreConditions,
            Logic = logic
        };
    }

    private TradingCondition MapPriceChangeCondition(ConditionItemViewModel uiCondition)
    {
        // 등락률 조건: "등락률: [일봉] 0봉 전 종가 대비 -7.0% ~ 0.0%"
        // Core에서는 ChangeRate 타입으로 매핑
        // 범위 조건이므로 두 개의 조건으로 분리 필요 (현재는 단순화)

        var value = ParseChangeRateValue(uiCondition.Description);

        return new TradingCondition
        {
            Name = "등락률 조건",
            Type = Core.Models.Trading.ConditionType.ChangeRate,
            Operator = ConditionOperator.LessThan,
            Value = value,
            Description = uiCondition.Description,
            IsEnabled = uiCondition.IsEnabled
        };
    }

    /// <summary>
    /// 등락률 파싱: "등락률: ... -7.0% ~ 0.0%" → -7.0
    /// </summary>
    private decimal ParseChangeRateValue(string description)
    {
        // 범위의 하한값 파싱 (예: -7.0%)
        var match = Regex.Match(description, @"(-?\d+(\.\d+)?)\s*%\s*~");
        if (match.Success)
        {
            return decimal.Parse(match.Groups[1].Value);
        }

        // 단일 값 파싱 (예: -7.0%)
        match = Regex.Match(description, @"(-?\d+(\.\d+)?)\s*%");
        if (match.Success)
        {
            return decimal.Parse(match.Groups[1].Value);
        }

        return 0m;
    }

    private TradingCondition MapMovingAverageCondition(ConditionItemViewModel uiCondition)
    {
        // 이동평균선 조건은 Core에 직접 대응하는 타입이 없음
        // 현재가 조건으로 근사화
        
        return new TradingCondition
        {
            Name = "이동평균선 조건",
            Type = Core.Models.Trading.ConditionType.Price,
            Operator = ConditionOperator.GreaterThan,
            Value = 0, // TODO: 이평선 계산 로직 필요
            Description = uiCondition.Description,
            IsEnabled = uiCondition.IsEnabled
        };
    }

    private TradingCondition MapTradeVolumeCondition(ConditionItemViewModel uiCondition)
    {
        // 거래대금 조건: "거래대금: 1000만 달러 이상"

        var value = ParseTradeAmountValue(uiCondition.Description);

        return new TradingCondition
        {
            Name = "거래대금 조건",
            Type = Core.Models.Trading.ConditionType.TradeAmount,
            Operator = ConditionOperator.GreaterThanOrEquals,
            Value = value,
            Description = uiCondition.Description,
            IsEnabled = uiCondition.IsEnabled
        };
    }

    /// <summary>
    /// 거래대금 파싱: "1000만 달러" → 10,000,000 / "1억 달러" → 100,000,000
    /// </summary>
    private decimal ParseTradeAmountValue(string description)
    {
        // 숫자 추출 (예: "1000")
        var numberMatch = Regex.Match(description, @"(\d+(\.\d+)?)");
        if (!numberMatch.Success)
            return 10_000_000m; // 기본값

        var number = decimal.Parse(numberMatch.Groups[1].Value);

        // 단위 확인
        if (description.Contains("조"))
            return number * 1_000_000_000_000m;
        else if (description.Contains("억"))
            return number * 100_000_000m;
        else if (description.Contains("만"))
            return number * 10_000m;
        else
            return number; // 단위 없으면 그대로
    }

    private TradingCondition MapPriceComparisonCondition(ConditionItemViewModel uiCondition)
    {
        // 주가 비교 조건: "(0봉 시가) > (1봉 저가)"
        // Core에 직접 대응하는 타입이 없으므로 현재가 조건으로 근사화
        
        return new TradingCondition
        {
            Name = "주가 비교 조건",
            Type = Core.Models.Trading.ConditionType.Price,
            Operator = ConditionOperator.GreaterThan,
            Value = 0, // TODO: 실제 비교 로직 필요
            Description = uiCondition.Description,
            IsEnabled = uiCondition.IsEnabled
        };
    }
}
