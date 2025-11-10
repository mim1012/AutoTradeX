using Newtonsoft.Json;

namespace AutoTrader.Core.DTOs.Auth;

/// <summary>
/// WebSocket approval_key 요청 DTO
/// API: POST /oauth2/Approval
/// </summary>
public class ApprovalKeyRequest
{
    /// <summary>
    /// 인증 타입 (고정값: "service")
    /// </summary>
    [JsonProperty("grant_type")]
    public string GrantType { get; set; } = "service";

    /// <summary>
    /// 앱 키 (KIS 발급)
    /// </summary>
    [JsonProperty("appkey")]
    public string AppKey { get; set; } = string.Empty;

    /// <summary>
    /// 앱 시크릿 키 (KIS 발급)
    /// </summary>
    [JsonProperty("appsecret")]
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// 팩토리 메서드: KIS 설정으로부터 approval key 요청 생성
    /// </summary>
    public static ApprovalKeyRequest FromCredentials(string appKey, string appSecret)
    {
        return new ApprovalKeyRequest
        {
            AppKey = appKey,
            AppSecret = appSecret
        };
    }
}
