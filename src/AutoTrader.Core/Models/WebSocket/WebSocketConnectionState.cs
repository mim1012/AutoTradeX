namespace AutoTrader.Core.Models.WebSocket;

/// <summary>
/// WebSocket 연결 상태
/// </summary>
public enum WebSocketConnectionState
{
    /// <summary>연결 끊김</summary>
    Disconnected = 0,

    /// <summary>연결 중</summary>
    Connecting = 1,

    /// <summary>연결됨</summary>
    Connected = 2,

    /// <summary>구독 중</summary>
    Subscribing = 3,

    /// <summary>구독 완료 (데이터 수신 중)</summary>
    Subscribed = 4,

    /// <summary>연결 해제 중</summary>
    Disconnecting = 5,

    /// <summary>오류 발생</summary>
    Error = 6
}
