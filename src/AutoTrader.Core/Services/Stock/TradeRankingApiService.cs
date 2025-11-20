using AutoTrader.Core.DTOs.Responses.TradeRanking;
using AutoTrader.Core.Services.Api;
using AutoTrader.Core.Services.Throttling;
using Microsoft.Extensions.Logging;

namespace AutoTrader.Core.Services.Stock;

/// <summary>
/// 시가총액 순위 API 서비스
/// TR ID: HHDFS76350100 (해외주식 시가총액순위)
/// </summary>
public class TradeRankingApiService
{
    private readonly IKisApiClient _apiClient;
    private readonly ILogger<TradeRankingApiService> _logger;

    // API 경로 및 TR ID (시가총액 순위로 변경)
    private const string ApiPath = "/uapi/overseas-stock/v1/ranking/market-cap";
    private const string TransactionId = "HHDFS76350100";

    public TradeRankingApiService(
        IKisApiClient apiClient,
        ILogger<TradeRankingApiService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Top N 미국 주식 조회 (시가총액 기준, 페이지네이션 지원)
    /// </summary>
    /// <param name="marketCode">시장 코드 (NAS: NASDAQ, NYS: NYSE)</param>
    /// <param name="count">조회 개수 (최대 300)</param>
    /// <returns>시가총액 순위 목록</returns>
    public async Task<List<TradeRankingItem>> GetTopStocksAsync(
        string marketCode = "NAS",
        int count = 300)
    {
        _logger.LogInformation("Fetching Top {Count} stocks from {Market}", count, marketCode);

        var allStocks = new List<TradeRankingItem>();
        var pageSize = 100; // API는 한 번에 100개까지만 반환
        var pagesToFetch = (int)Math.Ceiling(count / (double)pageSize);

        try
        {
            for (int page = 0; page < pagesToFetch && allStocks.Count < count; page++)
            {
                // 쿼리 파라미터 구성
                var queryParams = new Dictionary<string, string>
                {
                    { "AUTH", "" },
                    { "EXCD", marketCode },           // 거래소 코드 (NAS/NYS)
                    { "SRT_SQN", "0" },               // 정렬순서 (0: 시가총액 큰순)
                    { "VOL_RANG", "0" }               // 거래량 범위 (0: 전체)
                };

                // 2페이지부터는 KEYB 파라미터 추가 (마지막 종목 코드)
                if (page > 0 && allStocks.Count > 0)
                {
                    var lastStock = allStocks[^1];
                    queryParams["KEYB"] = lastStock.Symbol; // 마지막 종목 코드
                    _logger.LogDebug("Fetching page {Page} starting from {Symbol}", page + 1, lastStock.Symbol);
                }

                // 추가 헤더 (TR ID)
                var headers = new Dictionary<string, string>
                {
                    { "tr_id", TransactionId },
                    { "custtype", "P" }  // 개인
                };

                // API 호출
                var response = await _apiClient.GetAsync<TradeRankingResponse>(
                    ApiPath,
                    queryParams,
                    headers,
                    ApiPriority.Normal);

                if (response.Output2 == null || response.Output2.Count == 0)
                {
                    _logger.LogWarning("No more stocks available at page {Page}", page + 1);
                    break;
                }

                allStocks.AddRange(response.Output2);
                _logger.LogInformation("Fetched page {Page}: {Count} stocks (total: {Total})",
                    page + 1, response.Output2.Count, allStocks.Count);

                // 목표 개수에 도달하면 중단
                if (allStocks.Count >= count)
                {
                    break;
                }
            }

            // 요청한 개수만큼만 반환
            var result = allStocks.Take(count).ToList();
            _logger.LogInformation("Successfully fetched {Count} stocks from {Market}", result.Count, marketCode);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Top {Count} stocks from {Market}", count, marketCode);
            throw;
        }
    }

    /// <summary>
    /// 복수 시장에서 Top N 종목 조회 후 병합
    /// </summary>
    /// <param name="count">각 시장당 조회 개수 (기본값: 150, 두 시장 합쳐서 300개)</param>
    /// <returns>병합된 시가총액 순위 (최대 300개)</returns>
    public async Task<List<TradeRankingItem>> GetTop300FromAllMarketsAsync(int count = 150)
    {
        _logger.LogInformation("Fetching Top {Count} stocks from NASDAQ and NYSE (total: {Total})",
            count, count * 2);

        try
        {
            // NASDAQ과 NYSE에서 병렬로 조회
            var nasdaqTask = GetTopStocksAsync("NAS", count);
            var nyseTask = GetTopStocksAsync("NYS", count);

            await Task.WhenAll(nasdaqTask, nyseTask);

            var nasdaqStocks = nasdaqTask.Result;
            var nyseStocks = nyseTask.Result;

            // 병합 후 시가총액 기준으로 재정렬하여 상위 300개 선택
            var allStocks = nasdaqStocks.Concat(nyseStocks)
                .OrderByDescending(x => x.TradeAmount)
                .Take(300)
                .ToList();

            _logger.LogInformation("Merged {Total} stocks from NASDAQ ({Nasdaq}) and NYSE ({Nyse})",
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
