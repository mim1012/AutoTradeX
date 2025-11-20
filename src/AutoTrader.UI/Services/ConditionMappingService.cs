using AutoTrader.Core.Models.Trading;
using AutoTrader.Core.Models.Market;
using AutoTrader.UI.Models;
using AutoTrader.UI.ViewModels;
using System.Text.RegularExpressions;
using System.Text.Json;

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
        // 이동평균선 조건: Parameters에서 파라미터 추출
        var maType = MovingAverageType.MA10; // 기본값
        var maRangeMin = -10m; // 기본값
        var maRangeMax = 10m; // 기본값

        if (uiCondition.Parameters != null)
        {
            if (uiCondition.Parameters.TryGetValue("MaType", out var maTypeObj))
            {
                var maTypePeriod = Convert.ToInt32(maTypeObj);
                maType = maTypePeriod switch
                {
                    10 => MovingAverageType.MA10,
                    20 => MovingAverageType.MA20,
                    120 => MovingAverageType.MA120,
                    _ => MovingAverageType.MA10
                };
            }

            if (uiCondition.Parameters.TryGetValue("MaRangeMin", out var minObj))
            {
                maRangeMin = Convert.ToDecimal(minObj);
            }

            if (uiCondition.Parameters.TryGetValue("MaRangeMax", out var maxObj))
            {
                maRangeMax = Convert.ToDecimal(maxObj);
            }
        }

        return new TradingCondition
        {
            Name = "이동평균선 조건",
            Type = Core.Models.Trading.ConditionType.MovingAverage,
            MaType = maType,
            MaRangeMin = maRangeMin,
            MaRangeMax = maRangeMax,
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
        // 주가 비교 조건: Parameters에서 파라미터 추출
        var candleType = CandleType.Daily; // 기본값
        var candleOffsetA = 0; // 기본값
        var priceElementA = PriceElement.Close; // 기본값
        var op = ConditionOperator.GreaterThan; // 기본값
        var candleOffsetB = 1; // 기본값
        var priceElementB = PriceElement.Close; // 기본값

        if (uiCondition.Parameters != null)
        {
            if (uiCondition.Parameters.TryGetValue("CandleOffsetA", out var offsetAObj))
            {
                candleOffsetA = Convert.ToInt32(offsetAObj);
            }

            if (uiCondition.Parameters.TryGetValue("PriceElementA", out var elementAObj))
            {
                var elementAStr = elementAObj.ToString();
                priceElementA = elementAStr switch
                {
                    "Open" => PriceElement.Open,
                    "High" => PriceElement.High,
                    "Low" => PriceElement.Low,
                    "Close" => PriceElement.Close,
                    _ => PriceElement.Close
                };
            }

            if (uiCondition.Parameters.TryGetValue("Operator", out var opObj))
            {
                var opStr = opObj.ToString();
                op = opStr switch
                {
                    ">" => ConditionOperator.GreaterThan,
                    ">=" => ConditionOperator.GreaterThanOrEquals,
                    "<" => ConditionOperator.LessThan,
                    "<=" => ConditionOperator.LessThanOrEquals,
                    "==" => ConditionOperator.Equals,
                    _ => ConditionOperator.GreaterThan
                };
            }

            if (uiCondition.Parameters.TryGetValue("CandleOffsetB", out var offsetBObj))
            {
                candleOffsetB = Convert.ToInt32(offsetBObj);
            }

            if (uiCondition.Parameters.TryGetValue("PriceElementB", out var elementBObj))
            {
                var elementBStr = elementBObj.ToString();
                priceElementB = elementBStr switch
                {
                    "Open" => PriceElement.Open,
                    "High" => PriceElement.High,
                    "Low" => PriceElement.Low,
                    "Close" => PriceElement.Close,
                    _ => PriceElement.Close
                };
            }
        }

        return new TradingCondition
        {
            Name = "주가 비교 조건",
            Type = Core.Models.Trading.ConditionType.PriceComparison,
            CandleType = candleType,
            CandleOffsetA = candleOffsetA,
            PriceElementA = priceElementA,
            Operator = op,
            CandleOffsetB = candleOffsetB,
            PriceElementB = priceElementB,
            Description = uiCondition.Description,
            IsEnabled = uiCondition.IsEnabled
        };
    }
}
