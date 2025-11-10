using AutoTrader.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;

namespace AutoTrader.Core.Services.Throttling;

/// <summary>
/// API 호출 제한 서비스 구현
/// - 초당 20회 호출 제한 (SemaphoreSlim)
/// - 일일 1,200회 호출 제한
/// - 우선순위 큐 (Critical > High > Normal > Low)
/// - Polly 재시도 정책 (1s, 2s, 4s, 8s 백오프)
/// </summary>
public class ApiThrottler : IApiThrottler
{
    private readonly ApiThrottlingSettings _settings;
    private readonly ILogger<ApiThrottler> _logger;

    // 초당 호출 제한용 세마포어
    private readonly SemaphoreSlim _rateLimiter;

    // 우선순위 큐 (높은 우선순위부터 처리)
    private readonly PriorityQueue<IPendingRequest, int> _requestQueue = new();
    private readonly object _queueLock = new();

    // 일일 호출 카운터
    private int _todayCallCount;
    private DateTime _lastResetDate;
    private readonly object _counterLock = new();

    // 처리 루프 제어
    private Task? _processingTask;
    private readonly CancellationTokenSource _cts = new();
    private bool _isPaused;

    // Polly 재시도 정책
    private readonly AsyncRetryPolicy _retryPolicy;

    public ApiThrottler(
        IOptions<ApiThrottlingSettings> settings,
        ILogger<ApiThrottler> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 초당 MaxCallsPerSecond만큼 허용
        _rateLimiter = new SemaphoreSlim(_settings.MaxCallsPerSecond, _settings.MaxCallsPerSecond);

        _lastResetDate = DateTime.UtcNow.Date;

        // Polly 재시도 정책 생성
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                _settings.RetryCount,
                retryAttempt =>
                {
                    var backoffSeconds = _settings.BackoffSeconds[Math.Min(retryAttempt - 1, _settings.BackoffSeconds.Length - 1)];
                    return TimeSpan.FromSeconds(backoffSeconds);
                },
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "API call failed. Retry {RetryCount}/{MaxRetries} after {Delay}s: {Message}",
                        retryCount, _settings.RetryCount, timeSpan.TotalSeconds, exception.Message);
                });

        // 백그라운드 처리 루프 시작
        _processingTask = Task.Run(() => ProcessRequestsAsync(_cts.Token));

        _logger.LogInformation("ApiThrottler started (MaxCallsPerSecond: {Max}, DailyLimit: {Daily})",
            _settings.MaxCallsPerSecond, _settings.DailyCallLimit);
    }

    /// <inheritdoc/>
    public async Task<TResult> EnqueueAsync<TResult>(ApiRequest<TResult> request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        // 일일 제한 체크
        if (IsDailyLimitReached)
        {
            var ex = new InvalidOperationException(
                $"Daily API call limit reached ({_todayCallCount}/{_settings.DailyCallLimit})");
            _logger.LogError("API request rejected: {Message}", ex.Message);
            throw ex;
        }

        _logger.LogDebug("Enqueuing API request [{RequestId}] Priority: {Priority}, Description: {Description}",
            request.RequestId, request.Priority, request.Description);

        // 우선순위 큐에 추가
        lock (_queueLock)
        {
            // 높은 우선순위가 먼저 처리되도록 음수로 변환
            _requestQueue.Enqueue(
                new PendingRequest<TResult>(request.RequestId, request.ExecuteAsync, request.CompletionSource, request.Description, request.IsRetryable),
                -(int)request.Priority);
        }

        // 처리 완료 대기
        return await request.CompletionSource.Task;
    }

    /// <inheritdoc/>
    public int PendingRequestCount
    {
        get
        {
            lock (_queueLock)
            {
                return _requestQueue.Count;
            }
        }
    }

    /// <inheritdoc/>
    public int TodayCallCount
    {
        get
        {
            lock (_counterLock)
            {
                ResetDailyCounterIfNeeded();
                return _todayCallCount;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsDailyLimitReached
    {
        get
        {
            lock (_counterLock)
            {
                ResetDailyCounterIfNeeded();
                return _todayCallCount >= _settings.DailyCallLimit;
            }
        }
    }

    /// <inheritdoc/>
    public void Pause()
    {
        _isPaused = true;
        _logger.LogWarning("ApiThrottler paused");
    }

    /// <inheritdoc/>
    public void Resume()
    {
        _isPaused = false;
        _logger.LogInformation("ApiThrottler resumed");
    }

    /// <inheritdoc/>
    public bool IsRunning => !_isPaused && !_cts.Token.IsCancellationRequested;

    /// <summary>
    /// 백그라운드 요청 처리 루프
    /// </summary>
    private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("API request processing loop started");

        // 1초마다 세마포어 리셋하는 타이머
        var rateLimitResetTimer = new System.Timers.Timer(1000);
        rateLimitResetTimer.Elapsed += (_, _) =>
        {
            // 1초마다 세마포어를 MaxCallsPerSecond만큼 복원
            var currentCount = _rateLimiter.CurrentCount;
            var toRelease = _settings.MaxCallsPerSecond - currentCount;
            if (toRelease > 0)
            {
                _rateLimiter.Release(toRelease);
            }
        };
        rateLimitResetTimer.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // 일시 정지 상태면 대기
                if (_isPaused)
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                IPendingRequest? pendingRequest = null;

                // 큐에서 다음 요청 가져오기
                lock (_queueLock)
                {
                    if (_requestQueue.TryDequeue(out var request, out _))
                    {
                        pendingRequest = request;
                    }
                }

                if (pendingRequest == null)
                {
                    // 큐가 비어있으면 잠시 대기
                    await Task.Delay(50, cancellationToken);
                    continue;
                }

                // Rate limit 대기
                await _rateLimiter.WaitAsync(cancellationToken);

                try
                {
                    // 일일 카운터 증가
                    IncrementDailyCounter();

                    _logger.LogDebug("Executing API request [{RequestId}]: {Description}",
                        pendingRequest.RequestId, pendingRequest.Description);

                    // API 호출 실행 (재시도 정책 적용)
                    await pendingRequest.ExecuteAsync(_retryPolicy);

                    _logger.LogDebug("API request [{RequestId}] completed successfully",
                        pendingRequest.RequestId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "API request [{RequestId}] failed: {Message}",
                        pendingRequest.RequestId, ex.Message);
                    pendingRequest.SetException(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("API request processing loop cancelled");
        }
        finally
        {
            rateLimitResetTimer.Stop();
            rateLimitResetTimer.Dispose();
        }
    }

    /// <summary>
    /// 일일 카운터 증가
    /// </summary>
    private void IncrementDailyCounter()
    {
        lock (_counterLock)
        {
            ResetDailyCounterIfNeeded();
            _todayCallCount++;

            if (_todayCallCount % 100 == 0)
            {
                _logger.LogInformation("API call count today: {Count}/{Limit}",
                    _todayCallCount, _settings.DailyCallLimit);
            }
        }
    }

    /// <summary>
    /// 날짜가 바뀌면 일일 카운터 리셋
    /// </summary>
    private void ResetDailyCounterIfNeeded()
    {
        var today = DateTime.UtcNow.Date;
        if (_lastResetDate < today)
        {
            _logger.LogInformation("Resetting daily API call counter (previous: {Count})", _todayCallCount);
            _todayCallCount = 0;
            _lastResetDate = today;
        }
    }

    /// <summary>
    /// 리소스 정리
    /// </summary>
    public void Dispose()
    {
        _logger.LogInformation("ApiThrottler disposing...");

        _cts.Cancel();
        _processingTask?.Wait(TimeSpan.FromSeconds(5));

        _rateLimiter?.Dispose();
        _cts?.Dispose();

        _logger.LogInformation("ApiThrottler disposed");
    }

    /// <summary>
    /// 큐에 대기 중인 요청 인터페이스 (타입 소거용)
    /// </summary>
    private interface IPendingRequest
    {
        string RequestId { get; }
        string Description { get; }
        bool IsRetryable { get; }
        Task ExecuteAsync(AsyncRetryPolicy retryPolicy);
        void SetException(Exception ex);
    }

    /// <summary>
    /// 큐에 대기 중인 요청 정보 (제네릭)
    /// </summary>
    private class PendingRequest<TResult> : IPendingRequest
    {
        public string RequestId { get; }
        public string Description { get; }
        public bool IsRetryable { get; }
        private readonly Func<Task<TResult>> _executeAsync;
        private readonly TaskCompletionSource<TResult> _completionSource;

        public PendingRequest(
            string requestId,
            Func<Task<TResult>> executeAsync,
            TaskCompletionSource<TResult> completionSource,
            string description,
            bool isRetryable)
        {
            RequestId = requestId;
            _executeAsync = executeAsync;
            _completionSource = completionSource;
            Description = description;
            IsRetryable = isRetryable;
        }

        public async Task ExecuteAsync(AsyncRetryPolicy retryPolicy)
        {
            if (IsRetryable)
            {
                var result = await retryPolicy.ExecuteAsync(async () => await _executeAsync());
                _completionSource.SetResult(result);
            }
            else
            {
                var result = await _executeAsync();
                _completionSource.SetResult(result);
            }
        }

        public void SetException(Exception ex)
        {
            _completionSource.SetException(ex);
        }
    }
}
