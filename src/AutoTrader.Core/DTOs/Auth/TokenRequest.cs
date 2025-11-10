using Newtonsoft.Json;

namespace AutoTrader.Core.DTOs.Auth;

/// <summary>
/// OAuth 액세스 토큰 요청 DTO
/// API: POST /oauth2/tokenP
/// </summary>
public class TokenRequest
{
    /// <summary>
    /// 인증 타입 (고정값: "client_credentials")
    /// </summary>
    [JsonProperty("grant_type")]
    public string GrantType { get; set; } = "client_credentials";

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
    /// 팩토리 메서드: KIS 설정으로부터 토큰 요청 생성
    /// </summary>
    public static TokenRequest FromCredentials(string appKey, string appSecret)
    {
        return new TokenRequest
        {
            AppKey = appKey,
            AppSecret = appSecret
        };
    }
}
