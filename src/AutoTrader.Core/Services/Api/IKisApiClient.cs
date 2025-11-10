namespace AutoTrader.Core.Services.Api;

/// <summary>
/// KIS API 클라이언트 인터페이스
/// - HTTP 요청/응답 처리
/// - 자동 인증 헤더 설정
/// - Throttling 통합
/// </summary>
public interface IKisApiClient : IDisposable
{
    /// <summary>
    /// GET 요청 실행
    /// </summary>
    /// <typeparam name="TResponse">응답 타입</typeparam>
    /// <param name="path">API 경로 (예: /uapi/overseas-stock/v1/trading/inquire-balance)</param>
    /// <param name="queryParams">쿼리 파라미터</param>
    /// <param name="headers">추가 헤더</param>
    /// <param name="priority">요청 우선순위</param>
    /// <returns>API 응답 객체</returns>
    Task<TResponse> GetAsync<TResponse>(
        string path,
        Dictionary<string, string>? queryParams = null,
        Dictionary<string, string>? headers = null,
        Throttling.ApiPriority priority = Throttling.ApiPriority.Normal);

    /// <summary>
    /// POST 요청 실행
    /// </summary>
    /// <typeparam name="TRequest">요청 타입</typeparam>
    /// <typeparam name="TResponse">응답 타입</typeparam>
    /// <param name="path">API 경로</param>
    /// <param name="request">요청 본문</param>
    /// <param name="headers">추가 헤더</param>
    /// <param name="priority">요청 우선순위</param>
    /// <returns>API 응답 객체</returns>
    Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        Dictionary<string, string>? headers = null,
        Throttling.ApiPriority priority = Throttling.ApiPriority.Normal);

    /// <summary>
    /// 본문 없이 POST 요청 실행 (일부 API는 본문이 빈 POST)
    /// </summary>
    /// <typeparam name="TResponse">응답 타입</typeparam>
    /// <param name="path">API 경로</param>
    /// <param name="headers">추가 헤더</param>
    /// <param name="priority">요청 우청순위</param>
    /// <returns>API 응답 객체</returns>
    Task<TResponse> PostAsync<TResponse>(
        string path,
        Dictionary<string, string>? headers = null,
        Throttling.ApiPriority priority = Throttling.ApiPriority.Normal);
}
