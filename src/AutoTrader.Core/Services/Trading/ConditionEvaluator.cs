using AutoTrader.Core.Models.Realtime;
using AutoTrader.Core.Models.Trading;
using Microsoft.Extensions.Logging;

namespace AutoTrader.Core.Services.Trading;

/// <summary>
/// 거래 조건 평가 서비스 구현
/// - 단일/복합 조건 평가
/// - Parallel.ForEach로 300개 종목 병렬 평가
/// - AND/OR 로직 지원
/// </summary>
public class ConditionEvaluator : IConditionEvaluator
{
    private readonly ILogger<ConditionEvaluator> _logger;

    public ConditionEvaluator(ILogger<ConditionEvaluator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool EvaluateCondition(TradingCondition condition, CachedStockData stockData)
    {
        if (condition == null || stockData == null)
            return false;

        if (!condition.IsEnabled)
            return false;

        // 조건 타입별 값 추출
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
