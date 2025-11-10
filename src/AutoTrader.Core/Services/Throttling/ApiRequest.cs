namespace AutoTrader.Core.Services.Throttling;

/// <summary>
/// API 요청 우선순위
/// </summary>
public enum ApiPriority
{
    /// <summary>낮음: 조회 API (랭킹, 시세 등)</summary>
    Low = 0,

    /// <summary>보통: 일반 조회</summary>
    Normal = 1,

    /// <summary>높음: 계좌 조회</summary>
    High = 2,

    /// <summary>긴급: 주문 API (매수/매도)</summary>
    Critical = 3
}

/// <summary>
/// API 요청 래퍼 클래스
/// </summary>
/// <typeparam name="TResult">API 응답 타입</typeparam>
public class ApiRequest<TResult>
{
    /// <summary>
    /// 요청 ID (추적용)
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// 요청 우선순위
    /// </summary>
    public ApiPriority Priority { get; }

    /// <summary>
    /// 실제 API 호출 함수
    /// </summary>
    public Func<Task<TResult>> ExecuteAsync { get; }

    /// <summary>
    /// 요청 생성 시각
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// 요청 설명 (로깅용)
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 재시도 가능 여부 (기본값: true)
    /// </summary>
    public bool IsRetryable { get; }

    /// <summary>
    /// TaskCompletionSource (비동기 대기용)
    /// </summary>
    internal TaskCompletionSource<TResult> CompletionSource { get; }

    public ApiRequest(
        Func<Task<TResult>> executeAsync,
        ApiPriority priority = ApiPriority.Normal,
        string description = "",
        bool isRetryable = true)
    {
        RequestId = Guid.NewGuid().ToString("N");
        ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        Priority = priority;
        Description = description;
        IsRetryable = isRetryable;
        CreatedAt = DateTime.UtcNow;
        CompletionSource = new TaskCompletionSource<TResult>();
    }

    /// <summary>
    /// 대기 시간 (초)
    /// </summary>
    public double WaitingSeconds => (DateTime.UtcNow - CreatedAt).TotalSeconds;
}
