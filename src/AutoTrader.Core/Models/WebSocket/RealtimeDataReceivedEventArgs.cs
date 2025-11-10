using AutoTrader.Core.DTOs.WebSocket;

namespace AutoTrader.Core.Models.WebSocket;

/// <summary>
/// 실시간 데이터 수신 이벤트 인자
/// </summary>
public class RealtimeDataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 세션 ID
    /// </summary>
    public int SessionId { get; set; }

    /// <summary>
    /// 실시간 주식 데이터
    /// </summary>
    public RealtimeStockData Data { get; set; } = null!;

    /// <summary>
    /// 수신 시각 (UTC)
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public RealtimeDataReceivedEventArgs(int sessionId, RealtimeStockData data)
    {
        SessionId = sessionId;
        Data = data;
    }
}
