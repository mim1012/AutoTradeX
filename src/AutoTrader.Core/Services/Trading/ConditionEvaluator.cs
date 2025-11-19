using AutoTrader.Core.Models.Market;
using AutoTrader.Core.Models.Realtime;
using AutoTrader.Core.Models.Trading;
using AutoTrader.Core.Services.Market;
using Microsoft.Extensions.Logging;

namespace AutoTrader.Core.Services.Trading;

/// <summary>
/// 거래 조건 평가 서비스 구현
/// - 단일/복합 조건 평가
/// - Parallel.ForEach로 300개 종목 병렬 평가
/// - AND/OR 로직 지원
/// - MovingAverage 및 PriceComparison 조건 지원
/// </summary>
public class ConditionEvaluator : IConditionEvaluator
{
    private readonly ILogger<ConditionEvaluator> _logger;
    private readonly IHistoricalDataService? _historicalDataService;

    public ConditionEvaluator(
        ILogger<ConditionEvaluator> logger,
        IHistoricalDataService? historicalDataService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _historicalDataService = historicalDataService; // Optional for backward compatibility
    }

    /// <inheritdoc/>
    public bool EvaluateCondition(TradingCondition condition, CachedStockData stockData)
    {
        if (condition == null || stockData == null)
            return false;

        if (!condition.IsEnabled)
            return false;

        // 새로운 조건 타입: MovingAverage, PriceComparison
        if (condition.Type == ConditionType.MovingAverage)
        {
            return EvaluateMovingAverageCondition(condition, stockData);
        }
        else if (condition.Type == ConditionType.PriceComparison)
        {
            return EvaluatePriceComparisonCondition(condition, stockData);
        }

        // 기존 조건 타입별 값 추출
        var actualValue = condition.Type switch
        {
            ConditionType.Price => stockData.CurrentPrice,
            ConditionType.ChangeRate => stockData.ChangeRate,
            ConditionType.Volume => stockData.LatestData?.ExecutionVolume != null
                ? long.TryParse(stockData.LatestData.ExecutionVolume, out var vol) ? vol : 0
                : 0,
            ConditionType.TradeAmount => stockData.TradeAmount,
            _ => 0
        };

        // 비교 연산 수행
        return condition.Operator switch
        {
            ConditionOperator.GreaterThan => actualValue > condition.Value,
            ConditionOperator.LessThan => actualValue < condition.Value,
            ConditionOperator.Equals => actualValue == condition.Value,
            ConditionOperator.GreaterThanOrEquals => actualValue >= condition.Value,
            ConditionOperator.LessThanOrEquals => actualValue <= condition.Value,
            _ => false
        };
    }

    /// <summary>
    /// 이동평균선 조건 평가
    /// 예: 현재가가 20일 이평선 대비 -3% ~ +3% 이내
    /// </summary>
    private bool EvaluateMovingAverageCondition(TradingCondition condition, CachedStockData stockData)
    {
        if (_historicalDataService == null)
        {
            _logger.LogWarning("HistoricalDataService not available for MovingAverage condition");
            return false;
        }

        if (!condition.MaType.HasValue || !condition.MaRangeMin.HasValue || !condition.MaRangeMax.HasValue)
        {
            _logger.LogWarning("Invalid MovingAverage condition parameters");
            return false;
        }

        // 캔들 캐시 가져오기
        var candleCache = _historicalDataService.GetCandleCache(stockData.Symbol);
        if (candleCache == null)
        {
            _logger.LogDebug("No candle cache for {Symbol}", stockData.Symbol);
            return false;
        }

        // 이동평균 계산
        var ma = candleCache.CalculateMovingAverage(condition.MaType.Value);
        if (!ma.HasValue || ma.Value == 0)
        {
            _logger.LogDebug("Cannot calculate MA for {Symbol}", stockData.Symbol);
            return false;
        }

        // 현재가 대비 이동평균선 차이 (%)
        var currentPrice = stockData.CurrentPrice;
        var diffPercent = ((currentPrice - ma.Value) / ma.Value) * 100;

        // 범위 내에 있는지 체크
        bool result = diffPercent >= condition.MaRangeMin.Value && diffPercent <= condition.MaRangeMax.Value;

        _logger.LogTrace("{Symbol} MA{Period}: {MA:F2}, Current: {Price:F2}, Diff: {Diff:F2}%, Result: {Result}",
            stockData.Symbol, (int)condition.MaType.Value, ma.Value, currentPrice, diffPercent, result);

        return result;
    }

    /// <summary>
    /// 주가 비교 조건 평가
    /// 예: 0봉 종가 > 1봉 저가
    /// </summary>
    private bool EvaluatePriceComparisonCondition(TradingCondition condition, CachedStockData stockData)
    {
        if (_historicalDataService == null)
        {
            _logger.LogWarning("HistoricalDataService not available for PriceComparison condition");
            return false;
        }

        if (!condition.CandleType.HasValue || !condition.PriceElementA.HasValue || !condition.PriceElementB.HasValue)
        {
            _logger.LogWarning("Invalid PriceComparison condition parameters");
            return false;
        }

        // 캔들 캐시 가져오기
        var candleCache = _historicalDataService.GetCandleCache(stockData.Symbol);
        if (candleCache == null)
        {
            _logger.LogDebug("No candle cache for {Symbol}", stockData.Symbol);
            return false;
        }

        // n봉 전 캔들 가져오기
        var candleA = candleCache.GetCandle(condition.CandleType.Value, condition.CandleOffsetA);
        var candleB = candleCache.GetCandle(condition.CandleType.Value, condition.CandleOffsetB);

        if (candleA == null || candleB == null)
        {
            _logger.LogDebug("Cannot get candles for {Symbol} (offset A: {A}, B: {B})",
                stockData.Symbol, condition.CandleOffsetA, condition.CandleOffsetB);
            return false;
        }

        // 가격 요소 추출
        var priceA = candleA.GetPrice(condition.PriceElementA.Value);
        var priceB = candleB.GetPrice(condition.PriceElementB.Value);

        // 비교 연산
        bool result = condition.Operator switch
        {
            ConditionOperator.GreaterThan => priceA > priceB,
            ConditionOperator.LessThan => priceA < priceB,
            ConditionOperator.Equals => priceA == priceB,
            ConditionOperator.GreaterThanOrEquals => priceA >= priceB,
            ConditionOperator.LessThanOrEquals => priceA <= priceB,
            _ => false
        };

        _logger.LogTrace("{Symbol} {CandleA}{ElementA} ({PriceA:F2}) {Op} {CandleB}{ElementB} ({PriceB:F2}) = {Result}",
            stockData.Symbol,
            condition.CandleOffsetA, condition.PriceElementA.Value, priceA,
            condition.Operator,
            condition.CandleOffsetB, condition.PriceElementB.Value, priceB,
            result);

        return result;
    }

    /// <inheritdoc/>
    public bool EvaluateCompositeCondition(CompositeCondition compositeCondition, CachedStockData stockData)
    {
        if (compositeCondition == null || stockData == null)
            return false;

        var enabledConditions = compositeCondition.Conditions
            .Where(c => c.IsEnabled)
            .ToList();

        if (enabledConditions.Count == 0)
            return false;

        // AND 로직: 모든 조건 만족
        if (compositeCondition.Logic == ConditionLogic.And)
        {
            return enabledConditions.All(c => EvaluateCondition(c, stockData));
        }

        // OR 로직: 하나 이상 조건 만족
        return enabledConditions.Any(c => EvaluateCondition(c, stockData));
    }

    /// <inheritdoc/>
    public List<CachedStockData> EvaluateAllStocks(
        CompositeCondition compositeCondition,
        List<CachedStockData> allStockData)
    {
        if (compositeCondition == null || allStockData == null || allStockData.Count == 0)
            return new List<CachedStockData>();

        _logger.LogDebug("Evaluating {Count} stocks with condition: {Condition}",
            allStockData.Count, compositeCondition);

        var matchedStocks = new System.Collections.Concurrent.ConcurrentBag<CachedStockData>();

        // 병렬 처리로 300개 종목 평가
        Parallel.ForEach(allStockData, stockData =>
        {
            try
            {
                if (EvaluateCompositeCondition(compositeCondition, stockData))
                {
                    matchedStocks.Add(stockData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error evaluating condition for {Symbol}", stockData.Symbol);
            }
        });

        var results = matchedStocks.ToList();

        _logger.LogInformation("Condition evaluation complete: {Matched}/{Total} stocks matched",
            results.Count, allStockData.Count);

        return results;
    }

    /// <inheritdoc/>
    public List<CachedStockData> GetTopDecliningStocks(
        List<CachedStockData> allStockData,
        int topN = 2)
    {
        if (allStockData == null || allStockData.Count == 0)
            return new List<CachedStockData>();

        // 등락률 기준 오름차순 정렬 (음수가 더 낮음)
        var topDecliners = allStockData
            .Where(s => s.IsFresh) // 신선한 데이터만
            .OrderBy(s => s.ChangeRate) // 오름차순 (가장 낮은 등락률부터)
            .Take(topN)
            .ToList();

        _logger.LogInformation("Top {TopN} declining stocks: {Stocks}",
            topN,
            string.Join(", ", topDecliners.Select(s => $"{s.Symbol}({s.ChangeRate}%)")));

        return topDecliners;
    }
}
