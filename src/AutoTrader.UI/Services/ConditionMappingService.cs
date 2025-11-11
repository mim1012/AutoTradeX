using AutoTrader.Core.Models.Trading;
using AutoTrader.UI.Models;
using AutoTrader.UI.ViewModels;

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
        
        return new TradingCondition
        {
            Name = "등락률 조건",
            Type = Core.Models.Trading.ConditionType.ChangeRate,
            Operator = ConditionOperator.LessThan,
            Value = 0, // TODO: UI에서 실제 값 파싱 필요
            Description = uiCondition.Description,
            IsEnabled = uiCondition.IsEnabled
        };
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
        
        return new TradingCondition
        {
            Name = "거래대금 조건",
            Type = Core.Models.Trading.ConditionType.TradeAmount,
            Operator = ConditionOperator.GreaterThanOrEquals,
            Value = 10_000_000, // TODO: UI에서 실제 값 파싱 필요
            Description = uiCondition.Description,
            IsEnabled = uiCondition.IsEnabled
        };
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
