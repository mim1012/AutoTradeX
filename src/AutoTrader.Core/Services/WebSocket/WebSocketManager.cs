using AutoTrader.Core.Configuration;
using AutoTrader.Core.DTOs.Responses.TradeRanking;
using AutoTrader.Core.Models.WebSocket;
using AutoTrader.Core.Services.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoTrader.Core.Services.WebSocket;

/// <summary>
/// WebSocket 관리자 구현
/// - 8개 병렬 세션 관리 (300개 종목 ÷ 41 = 8세션)
/// - 각 세션당 최대 41개 종목
/// - 자동 재연결
/// - 실시간 데이터 집계
/// </summary>
public class WebSocketManager : IWebSocketManager
{
    private readonly IKisAuthService _authService;
    private readonly IOptions<KisSettings> _kisSettings;
    private readonly IOptions<WebSocketSettings> _wsSettings;
    private readonly ILogger<WebSocketManager> _logger;
    private readonly ILoggerFactory _loggerFactory;

    // 8개 세션
    private readonly List<WebSocketSession> _sessions = new();
    private readonly object _sessionsLock = new();

    // 재연결 타이머
    private System.Timers.Timer? _healthCheckTimer;

    /// <inheritdoc/>
    public event EventHandler<RealtimeDataReceivedEventArgs>? RealtimeDataReceived;

    public WebSocketManager(
        IKisAuthService authService,
        IOptions<KisSettings> kisSettings,
        IOptions<WebSocketSettings> wsSettings,
        ILogger<WebSocketManager> logger,
        ILoggerFactory loggerFactory)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _kisSettings = kisSettings ?? throw new ArgumentNullException(nameof(kisSettings));
        _wsSettings = wsSettings ?? throw new ArgumentNullException(nameof(wsSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc/>
    public async Task StartAllSessionsAsync(List<TradeRankingItem> stocks)
    {
        if (stocks.Count > 300)
        {
            throw new ArgumentException("Cannot subscribe to more than 300 stocks");
        }

        _logger.LogInformation("Starting {Count} WebSocket sessions for {StockCount} stocks",
            _wsSettings.Value.SessionCount, stocks.Count);

        // 종목을 세션별로 분할 (최대 41개씩)
        var stockGroups = SplitStocksIntoGroups(stocks, _wsSettings.Value.StocksPerSession);

        // 세션 생성 및 시작
        lock (_sessionsLock)
        {
            for (int i = 0; i < stockGroups.Count; i++)
            {
                var session = new WebSocketSession(
                    i,
                    _authService,
                    _kisSettings,
                    _wsSettings,
                    _loggerFactory.CreateLogger<WebSocketSession>());

                // 데이터 수신 이벤트 연결
                session.DataReceived += OnSessionDataReceived;

                _sessions.Add(session);
            }
        }

        // 모든 세션 시작
        var startTasks = new List<Task>();
        for (int i = 0; i < _sessions.Count; i++)
        {
            var session = _sessions[i];
            var stockGroup = stockGroups[i];

            startTasks.Add(session.StartAsync(stockGroup));

            // 세션 시작 간격 (서버 부하 방지)
            await Task.Delay(1000);
        }

        await Task.WhenAll(startTasks);

        // Health Check 타이머 시작
        StartHealthCheckTimer();

        _logger.LogInformation("All {Count} WebSocket sessions started successfully",
            _sessions.Count);
    }

    /// <inheritdoc/>
    public async Task StopAllSessionsAsync()
    {
        _logger.LogInformation("Stopping all WebSocket sessions");

        // Health Check 타이머 중지
        _healthCheckTimer?.Stop();
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;

        // 모든 세션 중지
        var stopTasks = _sessions.Select(s => s.StopAsync()).ToList();
        await Task.WhenAll(stopTasks);

        lock (_sessionsLock)
        {
            _sessions.Clear();
        }

        _logger.LogInformation("All WebSocket sessions stopped");
    }

    /// <inheritdoc/>
    public async Task RestartSessionAsync(int sessionId)
    {
        _logger.LogWarning("Restarting WebSocket session {SessionId}", sessionId);

        WebSocketSession? session;
        lock (_sessionsLock)
        {
            session = _sessions.FirstOrDefault(s => s.GetSessionInfo().SessionId == sessionId);
        }

        if (session == null)
        {
            _logger.LogError("Session {SessionId} not found", sessionId);
            return;
        }

        var sessionInfo = session.GetSessionInfo();
        var stocks = sessionInfo.SubscribedStocks;

        // 세션 중지
        await session.StopAsync();

        // 재연결 대기
        await Task.Delay(TimeSpan.FromSeconds(_wsSettings.Value.ReconnectDelaySeconds));

        // 세션 재시작
        try
        {
            await session.StartAsync(stocks);
            _logger.LogInformation("Session {SessionId} restarted successfully", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart session {SessionId}", sessionId);
            sessionInfo.ReconnectAttempts++;
        }
    }

    /// <inheritdoc/>
    public List<WebSocketSessionInfo> GetAllSessionInfo()
    {
        lock (_sessionsLock)
        {
            return _sessions.Select(s => s.GetSessionInfo()).ToList();
        }
    }

    /// <inheritdoc/>
    public WebSocketSessionInfo? GetSessionInfo(int sessionId)
    {
        lock (_sessionsLock)
        {
            var session = _sessions.FirstOrDefault(s => s.GetSessionInfo().SessionId == sessionId);
            return session?.GetSessionInfo();
        }
    }

    /// <inheritdoc/>
    public bool AreAllSessionsHealthy()
    {
        var allInfo = GetAllSessionInfo();
        return allInfo.All(info => info.IsHealthy);
    }

    /// <inheritdoc/>
    public int TotalSubscribedStockCount
    {
        get
        {
            var allInfo = GetAllSessionInfo();
            return allInfo.Sum(info => info.SubscribedStocks.Count);
        }
    }

    /// <summary>
    /// 세션 데이터 수신 이벤트 핸들러
    /// </summary>
    private void OnSessionDataReceived(object? sender, RealtimeDataReceivedEventArgs e)
    {
        // 상위로 이벤트 전파
        RealtimeDataReceived?.Invoke(this, e);
    }

    /// <summary>
    /// Health Check 타이머 시작 (60초마다 확인)
    /// </summary>
    private void StartHealthCheckTimer()
    {
        _healthCheckTimer = new System.Timers.Timer(60000); // 60초
        _healthCheckTimer.Elapsed += async (sender, e) =>
        {
            try
            {
                var allInfo = GetAllSessionInfo();
                foreach (var info in allInfo)
                {
                    if (!info.IsHealthy)
                    {
                        _logger.LogWarning(
                            "Session {SessionId} is unhealthy (State: {State}, LastData: {LastData})",
                            info.SessionId, info.State, info.LastDataReceivedAt);

                        // 재연결 시도
                        if (info.ReconnectAttempts < 3)
                        {
                            await RestartSessionAsync(info.SessionId);
                        }
                        else
                        {
                            _logger.LogError(
                                "Session {SessionId} exceeded max reconnect attempts ({Attempts})",
                                info.SessionId, info.ReconnectAttempts);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health check timer");
            }
        };

        _healthCheckTimer.Start();
        _logger.LogInformation("Health check timer started (interval: 60s)");
    }

    /// <summary>
    /// 종목을 그룹으로 분할
    /// </summary>
    private static List<List<TradeRankingItem>> SplitStocksIntoGroups(
        List<TradeRankingItem> stocks,
        int groupSize)
    {
        var groups = new List<List<TradeRankingItem>>();

        for (int i = 0; i < stocks.Count; i += groupSize)
        {
            var group = stocks.Skip(i).Take(groupSize).ToList();
            groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// 리소스 정리
    /// </summary>
    public void Dispose()
    {
        _healthCheckTimer?.Stop();
        _healthCheckTimer?.Dispose();

        lock (_sessionsLock)
        {
            foreach (var session in _sessions)
            {
                session.Dispose();
            }

            _sessions.Clear();
        }

        _logger.LogInformation("WebSocketManager disposed");
    }
}
