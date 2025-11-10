using AutoTrader.Core.Models.Realtime;
using AutoTrader.Core.Models.Trading;

namespace AutoTrader.Core.Services.Trading;

/// <summary>
/// 거래 조건 평가 서비스 인터페이스
/// - 단일/복합 조건 평가
/// - 300개 종목 병렬 평가
/// - AND/OR 로직 지원
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>
    /// 단일 조건 평가
    /// </summary>
    /// <param name="condition">평가할 조건</param>
    /// <param name="stockData">종목 실시간 데이터</param>
    /// <returns>조건 만족 여부</returns>
    bool EvaluateCondition(TradingCondition condition, CachedStockData stockData);

    /// <summary>
    /// 복합 조건 평가 (AND/OR)
    /// </summary>
    /// <param name="compositeCondition">복합 조건</param>
    /// <param name="stockData">종목 실시간 데이터</param>
    /// <returns>조건 만족 여부</returns>
    bool EvaluateCompositeCondition(CompositeCondition compositeCondition, CachedStockData stockData);

    /// <summary>
    /// 모든 종목에 대해 조건 평가 (병렬 처리)
    /// </summary>
    /// <param name="compositeCondition">평가할 조건</param>
    /// <param name="allStockData">모든 종목 데이터</param>
    /// <returns>조건을 만족하는 종목 목록</returns>
    List<CachedStockData> EvaluateAllStocks(
        CompositeCondition compositeCondition,
        List<CachedStockData> allStockData);

    /// <summary>
    /// Top N 하락 종목 조회 (등락률 기준)
    /// </summary>
    /// <param name="allStockData">모든 종목 데이터</param>
    /// <param name="topN">상위 N개 (기본값: 2)</param>
    /// <returns>하락률 상위 N개 종목</returns>
    List<CachedStockData> GetTopDecliningStocks(
        List<CachedStockData> allStockData,
        int topN = 2);
}
