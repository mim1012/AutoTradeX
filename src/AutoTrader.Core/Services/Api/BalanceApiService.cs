using AutoTrader.Core.Configuration;
using AutoTrader.Core.Services.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AutoTrader.Core.Services.Api;

/// <summary>
/// 한국투자증권 잔고 조회 API 서비스
/// </summary>
public class BalanceApiService
{
    private readonly IKisApiClient _apiClient;
    private readonly IKisAuthService _authService;
    private readonly KisSettings _settings;
    private readonly ILogger<BalanceApiService> _logger;

    public BalanceApiService(
        IKisApiClient apiClient,
        IKisAuthService authService,
        IOptions<KisSettings> settings,
        ILogger<BalanceApiService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 주문 가능 금액 조회 (미국 주식)
    /// </summary>
    public async Task<decimal> GetAvailableBalanceAsync(string accountNumber)
    {
        try
        {
            var acctNo = accountNumber ?? _settings.AccountNumber;

            // 계좌번호 포맷 (12345678-01 → 12345678, 01)
            var parts = acctNo.Split('-');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid account number format: {acctNo}. Expected format: XXXXXXXX-XX");

            var acctNum = parts[0];  // 계좌번호 앞 8자리
            var acctProdCd = parts[1]; // 계좌상품코드 뒤 2자리

            // Access Token 발급 (설정에서 AppKey/AppSecret 사용)
            var accessToken = await _authService.GetAccessTokenAsync();

            // API 호출
            var endpoint = "/uapi/overseas-stock/v1/trading/inquire-psamount";
            var trId = _settings.IsPaperTrading ? "VTTS3007R" : "TTTS3007R"; // 모의투자/실전투자

            var queryParams = new Dictionary<string, string>
            {
                { "CANO", acctNum },           // 종합계좌번호
                { "ACNT_PRDT_CD", acctProdCd }, // 계좌상품코드
                { "OVRS_EXCG_CD", "NASD" },    // 해외거래소코드 (나스닥)
                { "OVRS_ORD_UNPR", "0" },      // 해외주문단가 (0: 시장가)
                { "ITEM_CD", "" }               // 종목코드 (공백: 전체)
            };

            var headers = new Dictionary<string, string>
            {
                { "authorization", $"Bearer {accessToken}" },
                { "appkey", _settings.AppKey },
                { "appsecret", _settings.AppSecret },
                { "tr_id", trId }
            };

            var response = await _apiClient.GetAsync<string>(endpoint, queryParams, headers);

            if (response == null)
            {
                _logger.LogError("Balance API response is null");
                return 0m;
            }

            // 응답 파싱
            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            // 응답 코드 확인
            if (root.TryGetProperty("rt_cd", out var rtCd) && rtCd.GetString() != "0")
            {
                var errorMsg = root.TryGetProperty("msg1", out var msg1) ? msg1.GetString() : "Unknown error";
                _logger.LogError("Balance API error: {ErrorCode} - {ErrorMessage}", rtCd.GetString(), errorMsg);
                return 0m;
            }

            // output 데이터 추출
            if (root.TryGetProperty("output", out var output))
            {
                // 주문 가능 금액 (USD)
                if (output.TryGetProperty("ovrs_ord_psbl_amt", out var psblAmt))
                {
                    var amount = decimal.Parse(psblAmt.GetString() ?? "0");
                    _logger.LogInformation("Available balance: ${Amount:N2}", amount);
                    return amount;
                }

                // 대체: 예수금 (frcr_evlu_pfls_amt - 외화평가손익금액)
                if (output.TryGetProperty("frcr_buy_amt_smtl", out var buyAmt))
                {
                    var amount = decimal.Parse(buyAmt.GetString() ?? "0");
                    _logger.LogInformation("Available balance (alternative): ${Amount:N2}", amount);
                    return amount;
                }
            }

            _logger.LogWarning("Could not find available balance in API response");
            return 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available balance for account {AccountNumber}", accountNumber);
            throw;
        }
    }

    /// <summary>
    /// 계좌 잔고 조회 (미국 주식)
    /// </summary>
    public async Task<AccountBalanceInfo?> GetAccountBalanceAsync(string accountNumber)
    {
        try
        {
            var acctNo = accountNumber ?? _settings.AccountNumber;

            var parts = acctNo.Split('-');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid account number format: {acctNo}");

            var acctNum = parts[0];
            var acctProdCd = parts[1];

            var accessToken = await _authService.GetAccessTokenAsync();

            var endpoint = "/uapi/overseas-stock/v1/trading/inquire-balance";
            var trId = _settings.IsPaperTrading ? "VTTS3012R" : "TTTS3012R";

            var queryParams = new Dictionary<string, string>
            {
                { "CANO", acctNum },
                { "ACNT_PRDT_CD", acctProdCd },
                { "OVRS_EXCG_CD", "NASD" },
                { "TR_CRCY_CD", "USD" },       // 거래통화코드
                { "CTX_AREA_FK200", "" },
                { "CTX_AREA_NK200", "" }
            };

            var headers = new Dictionary<string, string>
            {
                { "authorization", $"Bearer {accessToken}" },
                { "appkey", _settings.AppKey },
                { "appsecret", _settings.AppSecret },
                { "tr_id", trId }
            };

            var response = await _apiClient.GetAsync<string>(endpoint, queryParams, headers);

            if (response == null)
                return null;

            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("rt_cd", out var rtCd) && rtCd.GetString() != "0")
            {
                var errorMsg = root.TryGetProperty("msg1", out var msg1) ? msg1.GetString() : "Unknown error";
                _logger.LogError("Balance inquiry error: {ErrorCode} - {ErrorMessage}", rtCd.GetString(), errorMsg);
                return null;
            }

            // output2 (계좌 요약 정보)
            if (root.TryGetProperty("output2", out var output2) && output2.ValueKind == JsonValueKind.Array)
            {
                var summary = output2[0];

                var balanceInfo = new AccountBalanceInfo
                {
                    TotalAssets = decimal.Parse(summary.GetProperty("tot_evlu_pfls_amt").GetString() ?? "0"),
                    AvailableCash = decimal.Parse(summary.GetProperty("frcr_evlu_tlex").GetString() ?? "0"),
                    TotalPurchaseAmount = decimal.Parse(summary.GetProperty("tot_pftrt").GetString() ?? "0"),
                    TotalEvaluationAmount = decimal.Parse(summary.GetProperty("evlu_pfls_smtl_amt").GetString() ?? "0")
                };

                _logger.LogInformation("Account balance: Total=${Total:N2}, Available=${Available:N2}",
                    balanceInfo.TotalAssets, balanceInfo.AvailableCash);

                return balanceInfo;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get account balance");
            throw;
        }
    }
}

/// <summary>
/// 계좌 잔고 정보
/// </summary>
public class AccountBalanceInfo
{
    /// <summary>총 자산 (USD)</summary>
    public decimal TotalAssets { get; set; }

    /// <summary>주문 가능 금액 (USD)</summary>
    public decimal AvailableCash { get; set; }

    /// <summary>총 매입 금액 (USD)</summary>
    public decimal TotalPurchaseAmount { get; set; }

    /// <summary>총 평가 금액 (USD)</summary>
    public decimal TotalEvaluationAmount { get; set; }

    /// <summary>평가 손익 (USD)</summary>
    public decimal ProfitLoss => TotalEvaluationAmount - TotalPurchaseAmount;

    /// <summary>수익률 (%)</summary>
    public decimal ProfitRate => TotalPurchaseAmount > 0
        ? (ProfitLoss / TotalPurchaseAmount) * 100m
        : 0m;

    public override string ToString()
    {
        return $"Total: ${TotalAssets:N2}, Available: ${AvailableCash:N2}, P/L: ${ProfitLoss:N2} ({ProfitRate:F2}%)";
    }
}
