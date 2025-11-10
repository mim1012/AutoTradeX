using AutoTrader.Core.DTOs.Responses.TradeRanking;

namespace AutoTrader.Core.Models.WebSocket;

/// <summary>
/// WebSocket 세션 정보
/// </summary>
public class WebSocketSessionInfo
{
    /// <summary>
    /// 세션 ID (0~7)
    /// </summary>
    public int SessionId { get; set; }

    /// <summary>
    /// 현재 연결 상태
    /// </summary>
    public WebSocketConnectionState State { get; set; }

    /// <summary>
    /// 구독 중인 종목 목록
    /// </summary>
    public List<TradeRankingItem> SubscribedStocks { get; set; } = new();

    /// <summary>
    /// 마지막 연결 시각
    /// </summary>
    public DateTime? LastConnectedAt { get; set; }

    /// <summary>
    /// 마지막 데이터 수신 시각
    /// </summary>
    public DateTime? LastDataReceivedAt { get; set; }

    /// <summary>
    /// 마지막 Heartbeat 전송 시각
    /// </summary>
    public DateTime? LastHeartbeatSentAt { get; set; }

    /// <summary>
    /// 재연결 시도 횟수
    /// </summary>
    public int ReconnectAttempts { get; set; }

    /// <summary>
    /// 총 수신 메시지 수
    /// </summary>
    public long TotalMessagesReceived { get; set; }

    /// <summary>
    /// 오류 메시지
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 연결 정상 여부 (60초 이내 데이터 수신)
    /// </summary>
    public bool IsHealthy =>
        State == WebSocketConnectionState.Subscribed &&
        LastDataReceivedAt.HasValue &&
        (DateTime.UtcNow - LastDataReceivedAt.Value).TotalSeconds < 60;
}
