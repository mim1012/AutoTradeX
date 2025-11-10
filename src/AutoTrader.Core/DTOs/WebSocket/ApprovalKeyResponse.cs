using Newtonsoft.Json;

namespace AutoTrader.Core.DTOs.WebSocket;

/// <summary>
/// WebSocket approval_key 	 Qı
/// API: POST /oauth2/Approval
/// </summary>
public class ApprovalKeyResponse
{
    /// <summary>
    /// WebSocket ç– ¨©` approval_key
    /// </summary>
    [JsonProperty("approval_key")]
    public string ApprovalKey { get; set; } = string.Empty;
}
