using Newtonsoft.Json;

namespace AutoTrader.Core.DTOs.Auth;

/// <summary>
/// WebSocket approval_key 발급 응답
/// API: POST /oauth2/Approval
/// </summary>
public class ApprovalKeyResponse
{
    /// <summary>
    /// WebSocket 연결용 approval_key
    /// </summary>
    [JsonProperty("approval_key")]
    public string ApprovalKey { get; set; } = string.Empty;
}
