using Newtonsoft.Json;

namespace AutoTrader.Core.DTOs.WebSocket;

/// <summary>
/// WebSocket l≈ Qı T‹¿
/// </summary>
public class WebSocketSubscribeResponse
{
    [JsonProperty("header")]
    public WebSocketResponseHeader? Header { get; set; }

    [JsonProperty("body")]
    public WebSocketResponseBody? Body { get; set; }
}

/// <summary>
/// WebSocket Qı ‰T
/// </summary>
public class WebSocketResponseHeader
{
    [JsonProperty("tr_id")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonProperty("tr_key")]
    public string TransactionKey { get; set; } = string.Empty;

    [JsonProperty("encrypt")]
    public string Encrypt { get; set; } = string.Empty;
}

/// <summary>
/// WebSocket Qı ¯8
/// </summary>
public class WebSocketResponseBody
{
    /// <summary>
    /// Qı T‹ ("0": 1ı)
    /// </summary>
    [JsonProperty("rt_cd")]
    public string ResponseCode { get; set; } = string.Empty;

    /// <summary>
    /// T‹¿ T‹
    /// </summary>
    [JsonProperty("msg_cd")]
    public string MessageCode { get; set; } = string.Empty;

    /// <summary>
    /// T‹¿ ¥© (: "SUBSCRIBE SUCCESS")
    /// </summary>
    [JsonProperty("msg1")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// T8T § Ù
    /// </summary>
    [JsonProperty("output")]
    public WebSocketResponseOutput? Output { get; set; }

    /// <summary>
    /// 1ı ÏÄ
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess => ResponseCode == "0";
}

/// <summary>
/// WebSocket Qı ú% (T8T §)
/// </summary>
public class WebSocketResponseOutput
{
    /// <summary>
    /// IV (Initialization Vector)
    /// </summary>
    [JsonProperty("iv")]
    public string Iv { get; set; } = string.Empty;

    /// <summary>
    /// T8T Key
    /// </summary>
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;
}
