using AutoTrader.Core.DTOs.Responses.TradeRanking;
using AutoTrader.Core.Services.Api;
using AutoTrader.Core.Services.Throttling;
using Microsoft.Extensions.Logging;

namespace AutoTrader.Core.Services.Stock;

/// <summary>
/// 거래량 순위 API 서비스
/// TR ID: HHDFS76320010 (해외주식 거래량순위)
/// </summary>
public class TradeRankingApiService
{
    private readonly IKisApiClient _apiClient;
    private readonly ILogger<TradeRankingApiService> _logger;

    // API 경로 및 TR ID
    private const string ApiPath = "/uapi/overseas-stock/v1/ranking/transaction";
    private const string TransactionId = "HHDFS76320010";

    public TradeRankingApiService(
        IKisApiClient apiClient,
        ILogger<TradeRankingApiService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Top 300 미국 주식 조회 (거래량 기준)
    /// </summary>
    /// <param name="marketCode">시장 코드 (NASD: NASDAQ, NYSE: NYSE, AMEX: AMEX)</param>
    /// <param name="count">조회 개수 (최대 300)</param>
    /// <returns>거래량 순위 응답</returns>
    public async Task<TradeRankingResponse> GetTop300StocksAsync(
        string marketCode = "NASD",
        int count = 300)
    {
        _logger.LogInformation("Fetching Top {Count} stocks from {Market}", count, marketCode);

        // 쿼리 파라미터 구성
        var queryParams = new Dictionary<string, string>
        {
            { "FID_COND_MRKT_DIV_CODE", "U" },         // 미국 시장
            { "FID_COND_SCR_DIV_CODE", "20171" },      // 거래량 순위
            { "FID_INPUT_ISCD", marketCode },          // 시장 코드
            { "FID_DIV_CLS_CODE", "0" },               // 전체
            { "FID_BLNG_CLS_CODE", "" },               // 소속 구분 (전체)
            { "FID_TRGT_CLS_CODE", "0" },              // 대상 구분 (전체)
            { "FID_TRGT_EXLS_CLS_CODE", "0" },         // 대상 제외 구분
            { "FID_INPUT_PRICE_1", "" },               // 입력 가격1
            { "FID_INPUT_PRICE_2", "" },               // 입력 가격2
            { "FID_VOL_CNT", "" },                     // 거래량 수
            { "FID_INPUT_DATE_1", "" }                 // 입력 날짜
        };

        // 추가 헤더 (TR ID)
        var headers = new Dictionary<string, string>
        {
            { "tr_id", TransactionId },
            { "custtype", "P" }  // 개인
        };

        try
        {
            // API 호출 (우선순위: Normal)
            var response = await _apiClient.GetAsync<TradeRankingResponse>(
                ApiPath,
                queryParams,
                headers,
                ApiPriority.Normal);

            _logger.LogInformation("Fetched {Count} stocks successfully", response.Items?.Count ?? 0);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Top 300 stocks from {Market}", marketCode);
            throw;
        }
    }

    /// <summary>
    /// 복수 시장에서 Top N 종목 조회 후 병합
    /// </summary>
    /// <param name="count">각 시장당 조회 개수</param>
    /// <returns>병합된 거래량 순위</returns>
    public async Task<List<TradeRankingItem>> GetTop300FromAllMarketsAsync(int count = 150)
    {
        _logger.LogInformation("Fetching Top {Count} stocks from NASDAQ and NYSE", count);

        try
        {
            // NASDAQ과 NYSE에서 병렬로 조회
            var nasdaqTask = GetTop300StocksAsync("NASD", count);
            var nyseTask = GetTop300StocksAsync("NYSE", count);

            await Task.WhenAll(nasdaqTask, nyseTask);

            var nasdaqStocks = nasdaqTask.Result.Items ?? new List<TradeRankingItem>();
            var nyseStocks = nyseTask.Result.Items ?? new List<TradeRankingItem>();

            // 병합 후 거래량 기준으로 재정렬
            var allStocks = nasdaqStocks.Concat(nyseStocks)
                .OrderByDescending(x => decimal.TryParse(x.TradeAmount, out var amt) ? amt : 0)
                .Take(300)
                .ToList();

            _logger.LogInformation("Merged {Count} stocks from NASDAQ {Nasdaq} and NYSE {Nyse}",
                allStocks.Count, nasdaqStocks.Count, nyseStocks.Count);

            return allStocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch and merge Top 300 stocks from all markets");
            throw;
        }
    }
}
