using AutoTrader.Core.DTOs.WebSocket;
using AutoTrader.Core.Models.Realtime;

namespace AutoTrader.Core.Services.Realtime;

/// <summary>
/// 실시간 데이터 집계 서비스 인터페이스
/// - WebSocket 데이터 수신 및 캐싱
/// - 종목별 최신 데이터 제공
/// - 데이터 신선도 체크
/// </summary>
public interface IRealtimeDataAggregator
{
    /// <summary>
    /// 실시간 데이터 업데이트
    /// </summary>
    /// <param name="data">실시간 주식 데이터</param>
    void UpdateData(RealtimeStockData data);

    /// <summary>
    /// 특정 종목의 최신 데이터 조회
    /// </summary>
    /// <param name="symbol">종목 심볼</param>
    /// <returns>캐시된 데이터 (없으면 null)</returns>
    CachedStockData? GetLatestData(string symbol);

    /// <summary>
    /// 모든 종목의 최신 데이터 조회
    /// </summary>
    /// <returns>캐시된 데이터 목록</returns>
    List<CachedStockData> GetAllData();

    /// <summary>
    /// 신선한 데이터만 조회 (10초 이내 업데이트)
    /// </summary>
    /// <returns>신선한 데이터 목록</returns>
    List<CachedStockData> GetFreshData();

    /// <summary>
    /// 캐시 초기화
    /// </summary>
    void ClearCache();

    /// <summary>
    /// 현재 캐시된 종목 수
    /// </summary>
    int CachedStockCount { get; }

    /// <summary>
    /// 신선한 데이터 종목 수 (10초 이내)
    /// </summary>
    int FreshStockCount { get; }

    /// <summary>
    /// 총 업데이트 횟수
    /// </summary>
    long TotalUpdates { get; }
}
