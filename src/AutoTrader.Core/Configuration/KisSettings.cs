namespace AutoTrader.Core.Configuration;

/// <summary>
/// \m,Œ API $
/// </summary>
public class KisSettings
{
    /// <summary>
    /// API Key (q¤)
    /// </summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>
    /// API Secret (q Ül¿)
    /// </summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// ÄŒˆ8 (: 12345678-01)
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// REST API Base URL
    /// </summary>
    public string BaseUrl { get; set; } = "https://openapi.koreainvestment.com:9443";

    /// <summary>
    /// WebSocket URL
    /// </summary>
    public string WebSocketUrl { get; set; } = "ws://ops.koreainvestment.com:21000";

    /// <summary>
    /// ¨X, ì€ (true: ¨X,, false: ä,)
    /// </summary>
    public bool IsPaperTrading { get; set; } = true;
}
