namespace AutoTrader.Core.Services.Throttling;

/// <summary>
/// API 호출 제한 서비스 인터페이스
/// - 초당 20회 호출 제한
/// - 일일 1,200회 호출 제한
/// - 우선순위 큐 (주문 > 조회)
/// - 자동 재시도 (Polly)
/// </summary>
public interface IApiThrottler : IDisposable
{
    /// <summary>
    /// API 요청을 큐에 추가하고 실행 대기
    /// </summary>
    /// <typeparam name="TResult">응답 타입</typeparam>
    /// <param name="request">API 요청 정보</param>
    /// <returns>API 응답 결과</returns>
    Task<TResult> EnqueueAsync<TResult>(ApiRequest<TResult> request);

    /// <summary>
    /// 현재 큐에 대기 중인 요청 수
    /// </summary>
    int PendingRequestCount { get; }

    /// <summary>
    /// 오늘 실행된 총 API 호출 수
    /// </summary>
    int TodayCallCount { get; }

    /// <summary>
    /// 일일 호출 제한 도달 여부
    /// </summary>
    bool IsDailyLimitReached { get; }

    /// <summary>
    /// Throttler 일시 정지 (긴급 상황)
    /// </summary>
    void Pause();

    /// <summary>
    /// Throttler 재개
    /// </summary>
    void Resume();

    /// <summary>
    /// Throttler 실행 중 여부
    /// </summary>
    bool IsRunning { get; }
}
