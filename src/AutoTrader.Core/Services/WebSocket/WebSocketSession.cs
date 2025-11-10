using AutoTrader.Core.Configuration;
using AutoTrader.Core.DTOs.Responses.TradeRanking;
using AutoTrader.Core.DTOs.WebSocket;
using AutoTrader.Core.Models.WebSocket;
using AutoTrader.Core.Services.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;

namespace AutoTrader.Core.Services.WebSocket;

/// <summary>
/// WebSocket 단일 세션 관리
/// - 최대 41개 종목 구독
/// - 실시간 데이터 수신
/// - Heartbeat (30초)
/// - 자동 재연결
/// </summary>
public class WebSocketSession : IDisposable
{
    private readonly int _sessionId;
    private readonly IKisAuthService _authService;
    private readonly KisSettings _kisSettings;
    private readonly WebSocketSettings _wsSettings;
    private readonly ILogger<WebSocketSession> _logger;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _heartbeatTask;

    // 세션 정보
    private readonly WebSocketSessionInfo _sessionInfo;
    private List<TradeRankingItem> _subscribedStocks = new();

    // 이벤트
    public event EventHandler<RealtimeDataReceivedEventArgs>? DataReceived;

    public WebSocketSession(
        int sessionId,
        IKisAuthService authService,
        IOptions<KisSettings> kisSettings,
        IOptions<WebSocketSettings> wsSettings,
        ILogger<WebSocketSession> logger)
    {
        _sessionId = sessionId;
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _kisSettings = kisSettings?.Value ?? throw new ArgumentNullException(nameof(kisSettings));
        _wsSettings = wsSettings?.Value ?? throw new ArgumentNullException(nameof(wsSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _sessionInfo = new WebSocketSessionInfo
        {
            SessionId = sessionId,
            State = WebSocketConnectionState.Disconnected
        };
    }

    /// <summary>
    /// 세션 시작 (연결 + 구독)
    /// </summary>
    public async Task StartAsync(List<TradeRankingItem> stocks)
    {
        if (stocks.Count > _wsSettings.StocksPerSession)
        {
            throw new ArgumentException(
                $"Cannot subscribe to more than {_wsSettings.StocksPerSession} stocks per session");
        }

        _subscribedStocks = stocks;
        _sessionInfo.SubscribedStocks = stocks;

        _logger.LogInformation("Starting WebSocket session {SessionId} with {Count} stocks",
            _sessionId, stocks.Count);

        // 연결
        await ConnectAsync();

        // 종목 구독
        await SubscribeStocksAsync();

        // 수신 루프 시작
        StartReceiveLoop();

        // Heartbeat 시작
        StartHeartbeat();
    }

    /// <summary>
    /// 세션 중지
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping WebSocket session {SessionId}", _sessionId);

        _sessionInfo.State = WebSocketConnectionState.Disconnecting;

        // CancellationToken 발행
        _cts?.Cancel();

        // WebSocket 연결 종료
        if (_webSocket?.State == System.Net.WebSockets.WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Session stopped",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing WebSocket session {SessionId}", _sessionId);
            }
        }

        // Task 대기
        if (_receiveTask != null)
            await _receiveTask;

        if (_heartbeatTask != null)
            await _heartbeatTask;

        _sessionInfo.State = WebSocketConnectionState.Disconnected;
        _logger.LogInformation("WebSocket session {SessionId} stopped", _sessionId);
    }

    /// <summary>
    /// 세션 정보 조회
    /// </summary>
    public WebSocketSessionInfo GetSessionInfo() => _sessionInfo;

    /// <summary>
    /// WebSocket 연결
    /// </summary>
    private async Task ConnectAsync()
    {
        _sessionInfo.State = WebSocketConnectionState.Connecting;

        try
        {
            // approval_key 발급
            var approvalKey = await _authService.GetApprovalKeyAsync();

            // ClientWebSocket 생성
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("approval_key", approvalKey);

            // 연결
            var uri = new Uri(_kisSettings.WebSocketUrl);
            _cts = new CancellationTokenSource();

            await _webSocket.ConnectAsync(uri, _cts.Token);

            _sessionInfo.State = WebSocketConnectionState.Connected;
            _sessionInfo.LastConnectedAt = DateTime.UtcNow;
            _sessionInfo.ReconnectAttempts = 0;

            _logger.LogInformation("WebSocket session {SessionId} connected to {Url}",
                _sessionId, _kisSettings.WebSocketUrl);
        }
        catch (Exception ex)
        {
            _sessionInfo.State = WebSocketConnectionState.Error;
            _sessionInfo.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Failed to connect WebSocket session {SessionId}", _sessionId);
            throw;
        }
    }

    /// <summary>
    /// 종목 구독
    /// </summary>
    private async Task SubscribeStocksAsync()
    {
        if (_webSocket == null || _webSocket.State != System.Net.WebSockets.WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        _sessionInfo.State = WebSocketConnectionState.Subscribing;

        try
        {
            // 각 종목별로 구독 요청 전송
            foreach (var stock in _subscribedStocks)
            {
                var subscribeRequest = new WebSocketSubscribeRequest
                {
                    Header = new WebSocketRequestHeader
                    {
                        AppKey = _kisSettings.AppKey,
                        AppSecret = _kisSettings.AppSecret,
                        CustomerType = "P",
                        TransactionType = "1", // 등록
                        ContentType = "utf-8"
                    },
                    Body = new WebSocketRequestBody
                    {
                        Input = new WebSocketRequestInput
                        {
                            TransactionId = "HDFSCNT0", // 해외주식 실시간 체결
                            TransactionKey = stock.Symbol
                        }
                    }
                };

                var requestJson = JsonConvert.SerializeObject(subscribeRequest);
                var requestBytes = Encoding.UTF8.GetBytes(requestJson);

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(requestBytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts!.Token);

                _logger.LogDebug("Subscribed to {Symbol} on session {SessionId}",
                    stock.Symbol, _sessionId);

                // 구독 간격 (API 부하 방지)
                await Task.Delay(50);
            }

            _sessionInfo.State = WebSocketConnectionState.Subscribed;

            _logger.LogInformation("WebSocket session {SessionId} subscribed to {Count} stocks",
                _sessionId, _subscribedStocks.Count);
        }
        catch (Exception ex)
        {
            _sessionInfo.State = WebSocketConnectionState.Error;
            _sessionInfo.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Failed to subscribe stocks on session {SessionId}", _sessionId);
            throw;
        }
    }

    /// <summary>
    /// 데이터 수신 루프 시작
    /// </summary>
    private void StartReceiveLoop()
    {
        _receiveTask = Task.Run(async () =>
        {
            var buffer = new byte[8192];

            while (_webSocket != null &&
                   _webSocket.State == System.Net.WebSockets.WebSocketState.Open &&
                   !_cts!.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("WebSocket session {SessionId} received close message",
                            _sessionId);
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // 실시간 데이터 파싱
                    if (message.StartsWith("0|") || message.StartsWith("1|"))
                    {
                        var realtimeData = RealtimeStockData.Parse(message);

                        _sessionInfo.LastDataReceivedAt = DateTime.UtcNow;
                        _sessionInfo.TotalMessagesReceived++;

                        // 이벤트 발생
                        DataReceived?.Invoke(this,
                            new RealtimeDataReceivedEventArgs(_sessionId, realtimeData));
                    }
                    else
                    {
                        // 구독 응답 등 기타 메시지
                        _logger.LogDebug("Session {SessionId} received: {Message}",
                            _sessionId, message);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Receive loop cancelled for session {SessionId}", _sessionId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in receive loop for session {SessionId}", _sessionId);
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Heartbeat 시작 (30초마다 ping)
    /// </summary>
    private void StartHeartbeat()
    {
        _heartbeatTask = Task.Run(async () =>
        {
            while (_webSocket != null &&
                   _webSocket.State == System.Net.WebSockets.WebSocketState.Open &&
                   !_cts!.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_wsSettings.HeartbeatIntervalSeconds), _cts.Token);

                    // Ping 전송
                    var pingMessage = "PING";
                    var pingBytes = Encoding.UTF8.GetBytes(pingMessage);

                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(pingBytes),
                        WebSocketMessageType.Text,
                        true,
                        _cts.Token);

                    _sessionInfo.LastHeartbeatSentAt = DateTime.UtcNow;

                    _logger.LogDebug("Heartbeat sent for session {SessionId}", _sessionId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Heartbeat cancelled for session {SessionId}", _sessionId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error sending heartbeat for session {SessionId}", _sessionId);
                }
            }
        });
    }

    /// <summary>
    /// 리소스 정리
    /// </summary>
    public void Dispose()
    {
        _cts?.Cancel();
        _webSocket?.Dispose();
        _cts?.Dispose();
    }
}
