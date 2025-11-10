using AutoTrader.Core.Configuration;
using AutoTrader.Core.DTOs.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace AutoTrader.Core.Services.Auth;

/// <summary>
/// KIS API 인증 서비스 구현
/// - OAuth 토큰 발급 및 캐싱
/// - WebSocket approval_key 발급
/// - 토큰 자동 갱신 (만료 5분 전)
/// - Thread-safe 토큰 관리
/// </summary>
public class KisAuthService : IKisAuthService
{
    private readonly HttpClient _httpClient;
    private readonly KisSettings _kisSettings;
    private readonly ILogger<KisAuthService> _logger;

    // 토큰 캐시 (thread-safe)
    private TokenResponse? _cachedToken;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    // approval_key 캐시 (WebSocket용)
    private string? _cachedApprovalKey;
    private readonly SemaphoreSlim _approvalKeyLock = new(1, 1);

    public KisAuthService(
        HttpClient httpClient,
        IOptions<KisSettings> kisSettings,
        ILogger<KisAuthService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _kisSettings = kisSettings?.Value ?? throw new ArgumentNullException(nameof(kisSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // HttpClient 기본 설정
        _httpClient.BaseAddress = new Uri(_kisSettings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync()
    {
        await _tokenLock.WaitAsync();
        try
        {
            // 캐시된 토큰이 유효하면 반환
            if (_cachedToken != null && _cachedToken.IsValid)
            {
                _logger.LogDebug("Using cached access token (expires in {Seconds}s)",
                    _cachedToken.RemainingSeconds);
                return _cachedToken.AccessToken;
            }

            // 새 토큰 발급
            _logger.LogInformation("Requesting new access token from KIS API");
            _cachedToken = await RequestNewTokenAsync();

            _logger.LogInformation("Access token acquired successfully (expires in {Seconds}s)",
                _cachedToken.ExpiresIn);

            return _cachedToken.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetApprovalKeyAsync()
    {
        await _approvalKeyLock.WaitAsync();
        try
        {
            // approval_key는 재사용 가능하므로 캐시
            if (!string.IsNullOrEmpty(_cachedApprovalKey))
            {
                _logger.LogDebug("Using cached approval_key");
                return _cachedApprovalKey;
            }

            _logger.LogInformation("Requesting new approval_key from KIS API");

            var request = ApprovalKeyRequest.FromCredentials(
                _kisSettings.AppKey,
                _kisSettings.AppSecret);

            var content = new StringContent(
                JsonConvert.SerializeObject(request),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/oauth2/Approval", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var approvalKeyResponse = JsonConvert.DeserializeObject<ApprovalKeyResponse>(responseBody)
                ?? throw new InvalidOperationException("Failed to deserialize approval_key response");

            _cachedApprovalKey = approvalKeyResponse.ApprovalKey;

            _logger.LogInformation("Approval_key acquired successfully");

            return _cachedApprovalKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire approval_key from KIS API");
            throw;
        }
        finally
        {
            _approvalKeyLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RefreshTokenAsync()
    {
        await _tokenLock.WaitAsync();
        try
        {
            _logger.LogInformation("Forcing access token refresh");

            // 기존 토큰 무효화
            _cachedToken = null;

            // 새 토큰 발급
            _cachedToken = await RequestNewTokenAsync();

            _logger.LogInformation("Access token refreshed successfully (expires in {Seconds}s)",
                _cachedToken.ExpiresIn);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <inheritdoc/>
    public bool IsTokenValid()
    {
        return _cachedToken != null && _cachedToken.IsValid;
    }

    /// <inheritdoc/>
    public int GetTokenRemainingSeconds()
    {
        return _cachedToken?.RemainingSeconds ?? 0;
    }

    /// <summary>
    /// KIS API로부터 새 액세스 토큰 요청
    /// </summary>
    private async Task<TokenResponse> RequestNewTokenAsync()
    {
        try
        {
            var request = TokenRequest.FromCredentials(
                _kisSettings.AppKey,
                _kisSettings.AppSecret);

            var content = new StringContent(
                JsonConvert.SerializeObject(request),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/oauth2/tokenP", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token request failed with status {StatusCode}: {Error}",
                    response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"Failed to acquire access token: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseBody)
                ?? throw new InvalidOperationException("Failed to deserialize token response");

            // 발급 시각 기록 (UTC)
            tokenResponse.IssuedAt = DateTime.UtcNow;

            return tokenResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire access token from KIS API");
            throw;
        }
    }

    /// <summary>
    /// 리소스 정리
    /// </summary>
    public void Dispose()
    {
        _tokenLock?.Dispose();
        _approvalKeyLock?.Dispose();
    }
}
