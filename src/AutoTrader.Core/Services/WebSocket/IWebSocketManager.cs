using AutoTrader.Core.DTOs.Responses.TradeRanking;
using AutoTrader.Core.Models.WebSocket;

namespace AutoTrader.Core.Services.WebSocket;

/// <summary>
/// WebSocket 관리자 인터페이스
/// - 8개 병렬 세션 관리
/// - 300개 종목 실시간 데이터 수신
/// - 자동 재연결 및 Heartbeat
/// </summary>
public interface IWebSocketManager : IDisposable
{
    /// <summary>
    /// 실시간 데이터 수신 이벤트
    /// </summary>
    event EventHandler<RealtimeDataReceivedEventArgs>? RealtimeDataReceived;

    /// <summary>
    /// 모든 세션 시작 (300개 종목 구독)
    /// </summary>
    /// <param name="stocks">구독할 종목 목록 (최대 300개)</param>
    Task StartAllSessionsAsync(List<TradeRankingItem> stocks);

    /// <summary>
    /// 모든 세션 중지
    /// </summary>
    Task StopAllSessionsAsync();

    /// <summary>
    /// 특정 세션 재시작
    /// </summary>
    /// <param name="sessionId">세션 ID (0~7)</param>
    Task RestartSessionAsync(int sessionId);

    /// <summary>
    /// 모든 세션 상태 조회
    /// </summary>
    List<WebSocketSessionInfo> GetAllSessionInfo();

    /// <summary>
    /// 특정 세션 상태 조회
    /// </summary>
    WebSocketSessionInfo? GetSessionInfo(int sessionId);

    /// <summary>
    /// 모든 세션이 정상 동작 중인지 확인
    /// </summary>
    bool AreAllSessionsHealthy();

    /// <summary>
    /// 현재 구독 중인 총 종목 수
    /// </summary>
    int TotalSubscribedStockCount { get; }
}
