namespace AutoTrader.Core.Services.Auth;

/// <summary>
/// KIS API 인증 서비스 인터페이스
/// OAuth 토큰 관리, WebSocket approval_key 발급, 토큰 갱신 등을 담당
/// </summary>
public interface IKisAuthService : IDisposable
{
    /// <summary>
    /// OAuth 액세스 토큰 획득
    /// - 캐시된 토큰이 유효하면 반환
    /// - 만료되었거나 없으면 새로 발급
    /// </summary>
    /// <returns>액세스 토큰</returns>
    Task<string> GetAccessTokenAsync();

    /// <summary>
    /// WebSocket 연결용 approval_key 획득
    /// API: POST /oauth2/Approval
    /// </summary>
    /// <returns>WebSocket approval_key</returns>
    Task<string> GetApprovalKeyAsync();

    /// <summary>
    /// 액세스 토큰 강제 갱신
    /// - 기존 토큰 무효화
    /// - 새 토큰 발급 및 캐시
    /// </summary>
    Task RefreshTokenAsync();

    /// <summary>
    /// 현재 캐시된 토큰의 유효성 검사
    /// </summary>
    /// <returns>토큰이 유효하면 true, 만료되었거나 없으면 false</returns>
    bool IsTokenValid();

    /// <summary>
    /// 토큰 만료 시간까지 남은 시간 (초 단위)
    /// </summary>
    /// <returns>남은 시간 (초), 토큰이 없으면 0</returns>
    int GetTokenRemainingSeconds();
}
