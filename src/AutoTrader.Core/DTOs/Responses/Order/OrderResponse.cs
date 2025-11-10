using Newtonsoft.Json;

namespace AutoTrader.Core.DTOs.Responses.Order;

/// <summary>
/// txüÝ ü8 Qõ
/// API: TTTT1002U (ømä)
/// </summary>
public class OrderResponse
{
    /// <summary>
    /// Qõ TÜ
    /// "0": 1õ, "1": ä(
    /// </summary>
    [JsonProperty("rt_cd")]
    public string ResponseCode { get; set; } = string.Empty;

    /// <summary>
    /// TÜÀ TÜ
    /// </summary>
    [JsonProperty("msg_cd")]
    public string MessageCode { get; set; } = string.Empty;

    /// <summary>
    /// Qõ TÜÀ
    /// </summary>
    [JsonProperty("msg1")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// ü8 °ü ô
    /// </summary>
    [JsonProperty("output")]
    public OrderOutput? Output { get; set; }

    /// <summary>
    /// 1õ ì€
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess => ResponseCode == "0";

    /// <summary>
    /// ä( ì€
    /// </summary>
    [JsonIgnore]
    public bool IsFailed => ResponseCode == "1";
}
