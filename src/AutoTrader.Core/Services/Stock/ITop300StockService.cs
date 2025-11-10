using AutoTrader.Core.DTOs.Responses.TradeRanking;

namespace AutoTrader.Core.Services.Stock;

/// <summary>
/// Top 300 미국 주식 조회 서비스 인터페이스
/// - 1분마다 거래량 상위 300개 종목 조회
/// - 조회 결과 메모리 캐시
/// - WebSocket 연결 대상 제공
/// </summary>
public interface ITop300StockService
{
    /// <summary>
    /// Top 300 종목 새로 조회 및 캐시 갱신
    /// - KIS API 호출 (HHDFS76320010)
    /// - 메모리 캐시 업데이트
    /// </summary>
    Task RefreshTop300Async();

    /// <summary>
    /// 현재 캐시된 Top 300 종목 목록 반환
    /// </summary>
    /// <returns>종목 목록 (최대 300개)</returns>
    List<TradeRankingItem> GetCachedTop300();

    /// <summary>
    /// 특정 종목이 Top 300에 포함되는지 확인
    /// </summary>
    /// <param name="symbol">종목 심볼 (예: AAPL)</param>
    /// <returns>포함 여부</returns>
    bool IsInTop300(string symbol);

    /// <summary>
    /// 마지막 갱신 시각
    /// </summary>
    DateTime? LastRefreshTime { get; }

    /// <summary>
    /// 캐시된 종목 수
    /// </summary>
    int CachedStockCount { get; }
}
