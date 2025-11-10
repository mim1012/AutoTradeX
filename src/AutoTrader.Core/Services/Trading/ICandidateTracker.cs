using AutoTrader.Core.Models.Realtime;
using AutoTrader.Core.Models.Trading;

namespace AutoTrader.Core.Services.Trading;

/// <summary>
/// 매수 후보 종목 추적 서비스 인터페이스
/// - 2회 연속 조건 확인 (10초 간격)
/// - 최종 후보 선정
/// </summary>
public interface ICandidateTracker
{
    /// <summary>
    /// 조건 만족 종목 추가/업데이트
    /// - 첫 확인: 후보로 등록
    /// - 두 번째 확인: 최종 확정
    /// </summary>
    /// <param name="stockData">종목 데이터</param>
    void TrackCandidate(CachedStockData stockData);

    /// <summary>
    /// 여러 종목을 한번에 추적
    /// </summary>
    /// <param name="stockDataList">종목 데이터 목록</param>
    void TrackCandidates(List<CachedStockData> stockDataList);

    /// <summary>
    /// 2회 확인 완료된 최종 후보 조회
    /// </summary>
    /// <returns>확정된 후보 목록</returns>
    List<CandidateStock> GetConfirmedCandidates();

    /// <summary>
    /// 1회만 확인된 임시 후보 조회
    /// </summary>
    /// <returns>임시 후보 목록</returns>
    List<CandidateStock> GetPendingCandidates();

    /// <summary>
    /// 모든 후보 조회 (확정 + 임시)
    /// </summary>
    /// <returns>전체 후보 목록</returns>
    List<CandidateStock> GetAllCandidates();

    /// <summary>
    /// 만료된 임시 후보 제거 (20초 초과)
    /// </summary>
    /// <returns>제거된 후보 수</returns>
    int RemoveExpiredCandidates();

    /// <summary>
    /// 후보 초기화
    /// </summary>
    void ClearCandidates();

    /// <summary>
    /// 현재 확정 후보 수
    /// </summary>
    int ConfirmedCandidateCount { get; }

    /// <summary>
    /// 현재 임시 후보 수
    /// </summary>
    int PendingCandidateCount { get; }
}
