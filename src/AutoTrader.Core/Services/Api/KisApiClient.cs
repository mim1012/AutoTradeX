using AutoTrader.Core.Configuration;
using AutoTrader.Core.Services.Auth;
using AutoTrader.Core.Services.Throttling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace AutoTrader.Core.Services.Api;

/// <summary>
/// KIS API 클라이언트 구현
/// - HttpClient 기반 요청 처리
/// - 자동 인증 토큰 헤더 설정
/// - API Throttler 통합으로 호출 제한 준수
/// - GET/POST 메서드 제공
/// </summary>
public class KisApiClient : IKisApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IKisAuthService _authService;
    private readonly IApiThrottler _throttler;
    private readonly KisSettings _kisSettings;
    private readonly ILogger<KisApiClient> _logger;

    public KisApiClient(
        HttpClient httpClient,
        IKisAuthService authService,
        IApiThrottler throttler,
        IOptions<KisSettings> kisSettings,
        ILogger<KisApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));
        _kisSettings = kisSettings?.Value ?? throw new ArgumentNullException(nameof(kisSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // HttpClient 기본 설정
        _httpClient.BaseAddress = new Uri(_kisSettings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <inheritdoc/>
    public async Task<TResponse> GetAsync<TResponse>(
        string path,
        Dictionary<string, string>? queryParams = null,
        Dictionary<string, string>? headers = null,
        ApiPriority priority = ApiPriority.Normal)
    {
        // 쿼리 파라미터 구성
        var queryString = BuildQueryString(queryParams);
        var fullPath = string.IsNullOrEmpty(queryString) ? path : $"{path}?{queryString}";

        _logger.LogDebug("GET request: {Path}", fullPath);

        // API 요청 생성
        var apiRequest = new ApiRequest<TResponse>(
            executeAsync: async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, fullPath);

                // 헤더 설정
                await SetAuthHeadersAsync(request, headers);

                // HTTP 요청 실행
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TResponse>(responseBody)
                    ?? throw new InvalidOperationException($"Failed to deserialize response from {path}");

                return result;
            },
            priority: priority,
            description: $"GET {path}",
            isRetryable: true);

        // Throttler를 통해 실행 (Rate Limiting 적용)
        return await _throttler.EnqueueAsync(apiRequest);
    }

    /// <inheritdoc/>
    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        Dictionary<string, string>? headers = null,
        ApiPriority priority = ApiPriority.Normal)
    {
        _logger.LogDebug("POST request: {Path}", path);

        // API 요청 생성
        var apiRequest = new ApiRequest<TResponse>(
            executeAsync: async () =>
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);

                // 헤더 설정
                await SetAuthHeadersAsync(httpRequest, headers);

                // 요청 본문 설정
                var requestBody = JsonConvert.SerializeObject(request);
                httpRequest.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // HTTP 요청 실행
                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TResponse>(responseBody)
                    ?? throw new InvalidOperationException($"Failed to deserialize response from {path}");

                return result;
            },
            priority: priority,
            description: $"POST {path}",
            isRetryable: true);

        // Throttler를 통해 실행
        return await _throttler.EnqueueAsync(apiRequest);
    }

    /// <inheritdoc/>
    public async Task<TResponse> PostAsync<TResponse>(
        string path,
        Dictionary<string, string>? headers = null,
        ApiPriority priority = ApiPriority.Normal)
    {
        _logger.LogDebug("POST request (no body): {Path}", path);

        // API 요청 생성
        var apiRequest = new ApiRequest<TResponse>(
            executeAsync: async () =>
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);

                // 헤더 설정
                await SetAuthHeadersAsync(httpRequest, headers);

                // 빈 본문 설정
                httpRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                // HTTP 요청 실행
                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TResponse>(responseBody)
                    ?? throw new InvalidOperationException($"Failed to deserialize response from {path}");

                return result;
            },
            priority: priority,
            description: $"POST {path} (no body)",
            isRetryable: true);

        // Throttler를 통해 실행
        return await _throttler.EnqueueAsync(apiRequest);
    }

    /// <summary>
    /// 인증 헤더 설정 (OAuth 토큰 + 추가 헤더)
    /// </summary>
    private async Task SetAuthHeadersAsync(HttpRequestMessage request, Dictionary<string, string>? additionalHeaders)
    {
        // OAuth 토큰 헤더
        var accessToken = await _authService.GetAccessTokenAsync();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // AppKey, AppSecret 헤더 (KIS API 요구사항)
        request.Headers.Add("appkey", _kisSettings.AppKey);
        request.Headers.Add("appsecret", _kisSettings.AppSecret);

        // 추가 헤더 설정
        if (additionalHeaders != null)
        {
            foreach (var (key, value) in additionalHeaders)
            {
                request.Headers.Add(key, value);
            }
        }

        _logger.LogDebug("Authorization header set (token expires in {Seconds}s)",
            _authService.GetTokenRemainingSeconds());
    }

    /// <summary>
    /// 쿼리 파라미터 문자열 구성
    /// </summary>
    private static string BuildQueryString(Dictionary<string, string>? queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
            return string.Empty;

        var parameters = queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");

        return string.Join("&", parameters);
    }

    /// <summary>
    /// 리소스 정리
    /// </summary>
    public void Dispose()
    {
        // HttpClient는 HttpClientFactory에서 관리하므로 Dispose 하지 않음
        _logger.LogDebug("KisApiClient disposed");
    }
}
