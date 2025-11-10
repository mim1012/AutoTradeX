using Newtonsoft.Json;

namespace AutoTrader.Core.DTOs.Auth;

/// <summary>
/// OAuth 액세스 토큰 응답 DTO
/// API: POST /oauth2/tokenP
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// 액세스 토큰 (API 호출 시 헤더에 사용)
    /// </summary>
    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 토큰 타입 (일반적으로 "Bearer")
    /// </summary>
    [JsonProperty("token_type")]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// 토큰 유효 기간 (초 단위)
    /// 기본값: 86400초 (24시간)
    /// </summary>
    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// 액세스 토큰 만료 일시
    /// 형식: "2024-01-01 00:00:00"
    /// </summary>
    [JsonProperty("access_token_token_expired")]
    public string AccessTokenExpired { get; set; } = string.Empty;

    /// <summary>
    /// 토큰 발급 시각 (로컬에서 기록)
    /// </summary>
    [JsonIgnore]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 토큰 만료 시각 계산 (IssuedAt + ExpiresIn)
    /// </summary>
    [JsonIgnore]
    public DateTime ExpiresAt => IssuedAt.AddSeconds(ExpiresIn);

    /// <summary>
    /// 토큰이 현재 유효한지 확인
    /// - 만료 5분 전까지를 유효로 간주 (안전 마진)
    /// </summary>
    [JsonIgnore]
    public bool IsValid => DateTime.UtcNow < ExpiresAt.AddMinutes(-5);

    /// <summary>
    /// 토큰 만료까지 남은 시간 (초 단위)
    /// </summary>
    [JsonIgnore]
    public int RemainingSeconds => Math.Max(0, (int)(ExpiresAt - DateTime.UtcNow).TotalSeconds);
}
