using AutoTrader.Core.Configuration;
using AutoTrader.Core.DTOs.Requests.Order;
using AutoTrader.Core.DTOs.Responses.Order;
using AutoTrader.Core.Services.Api;
using AutoTrader.Core.Services.Throttling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoTrader.Core.Services.Trading;

/// <summary>
/// 주문 API 서비스
/// TR ID: TTTT1002U (해외주식 주문)
/// </summary>
public class OrderApiService
{
    private readonly IKisApiClient _apiClient;
    private readonly BalanceApiService _balanceService;
    private readonly KisSettings _kisSettings;
    private readonly ILogger<OrderApiService> _logger;

    // API 경로 및 TR ID
    private const string ApiPath = "/uapi/overseas-stock/v1/trading/order";
    private const string TransactionId = "TTTT1002U"; // 해외주식 매수 주문

    public OrderApiService(
        IKisApiClient apiClient,
        BalanceApiService balanceService,
        IOptions<KisSettings> kisSettings,
        ILogger<OrderApiService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _balanceService = balanceService ?? throw new ArgumentNullException(nameof(balanceService));
        _kisSettings = kisSettings?.Value ?? throw new ArgumentNullException(nameof(kisSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// LOC 매수 주문 실행
    /// </summary>
    /// <param name="symbol">종목 심볼</param>
    /// <param name="quantity">주문 수량</param>
    /// <param name="locPrice">LOC 주문 가격</param>
    /// <param name="exchangeCode">거래소 코드 (NASD: NASDAQ, NYSE: NYSE)</param>
    /// <returns>주문 응답</returns>
    public async Task<OrderResponse> PlaceLocBuyOrderAsync(
        string symbol,
        int quantity,
        decimal locPrice,
        string exchangeCode = "NASD")
    {
        _logger.LogInformation("Placing LOC buy order: {Symbol} x{Quantity} @ ${Price}",
            symbol, quantity, locPrice);

        // 주문 요청 생성
        var orderRequest = OrderRequest.CreateLocBuyOrder(
            _kisSettings.AccountNumber,
            symbol,
            quantity,
            locPrice,
            exchangeCode);

        // 추가 헤더 (TR ID)
        var headers = new Dictionary<string, string>
        {
            { "tr_id", TransactionId },
            { "custtype", "P" }  // 개인
        };

        try
        {
            // API 호출 (우선순위: Critical - 주문은 최우선)
            var response = await _apiClient.PostAsync<OrderRequest, OrderResponse>(
                ApiPath,
                orderRequest,
                headers,
                ApiPriority.Critical);

            if (response.IsSuccess)
            {
                _logger.LogInformation("Order placed successfully: {Symbol} (Order#: {OrderNumber})",
                    symbol, response.Output?.OrderNumber);
            }
            else
            {
                _logger.LogWarning("Order failed: {Symbol} - {Message} (Code: {Code})",
                    symbol, response.Message, response.ResponseCode);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place order for {Symbol}", symbol);
            throw;
        }
    }

    /// <summary>
    /// 계좌 잔고 조회 (주문 가능 금액)
    /// </summary>
    /// <param name="accountNumber">계좌번호 (선택, null이면 설정 파일의 계좌 사용)</param>
    /// <returns>USD 주문 가능 금액</returns>
    public async Task<decimal> GetAccountBalanceAsync(string? accountNumber = null)
    {
        try
        {
            var acctNo = accountNumber ?? _kisSettings.AccountNumber;
            var balance = await _balanceService.GetAvailableBalanceAsync(acctNo);

            _logger.LogInformation("Account balance retrieved: ${Balance:N2}", balance);
            return balance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get account balance, falling back to default value");

            // API 실패 시 설정된 기본값 사용 (안전장치)
            const decimal fallbackBalance = 10000m;
            _logger.LogWarning("Using fallback balance: ${Balance:N2}", fallbackBalance);
            return fallbackBalance;
        }
    }
}
