using AutoTrader.Core.DTOs.Responses.TradeRanking;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AutoTrader.Core.Services.Stock;

/// <summary>
/// Top 300 미국 주식 관리 서비스
/// - 1분마다 거래량 상위 300개 종목 조회
/// - Thread-safe 메모리 캐시
/// - WebSocket 구독 대상 제공
/// </summary>
public class Top300StockService : ITop300StockService, IDisposable
{
    private readonly TradeRankingApiService _rankingApi;
    private readonly ILogger<Top300StockService> _logger;

    // Thread-safe 캐시
    private readonly ConcurrentDictionary<string, TradeRankingItem> _stockCache = new();
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    private DateTime? _lastRefreshTime;
    private bool _disposed;

    public Top300StockService(
        TradeRankingApiService rankingApi,
        ILogger<Top300StockService> logger)
    {
        _rankingApi = rankingApi ?? throw new ArgumentNullException(nameof(rankingApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task RefreshTop300Async()
    {
        // SemaphoreSlim을 사용한 비동기 잠금
        await _refreshSemaphore.WaitAsync();

        try
        {
            // 중복 갱신 방지 (1분 이내 재호출 시 스킵)
            if (_lastRefreshTime.HasValue &&
                (DateTime.UtcNow - _lastRefreshTime.Value).TotalSeconds < 55)
            {
                _logger.LogDebug("Skipping refresh (last refresh was {Seconds}s ago)",
                    (DateTime.UtcNow - _lastRefreshTime.Value).TotalSeconds);
                return;
            }

            _logger.LogInformation("Refreshing Top 300 stocks...");

            // NASDAQ + NYSE에서 Top 150씩 조회 후 병합
            var top300Stocks = await _rankingApi.GetTop300FromAllMarketsAsync(count: 150);

            // 캐시 업데이트
            _stockCache.Clear();

            foreach (var stock in top300Stocks)
            {
                _stockCache[stock.Symbol] = stock;
            }

            _lastRefreshTime = DateTime.UtcNow;

            _logger.LogInformation("Top 300 stocks refreshed successfully ({Count} stocks cached)",
                _stockCache.Count);

            // 상위 10개 로깅
            var top10 = top300Stocks.Take(10).ToList();
            _logger.LogDebug("Top 10 stocks: {Symbols}",
                string.Join(", ", top10.Select(s => $"{s.Symbol}(#{s.Rank})")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Top 300 stocks");
            throw;
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public List<TradeRankingItem> GetCachedTop300()
    {
        return _stockCache.Values
            .OrderBy(x => x.Rank)
            .ToList();
    }

    /// <inheritdoc/>
    public bool IsInTop300(string symbol)
    {
        return _stockCache.ContainsKey(symbol);
    }

    /// <inheritdoc/>
    public DateTime? LastRefreshTime => _lastRefreshTime;

    /// <inheritdoc/>
    public int CachedStockCount => _stockCache.Count;

    /// <summary>
    /// Dispose 패턴 구현 - SemaphoreSlim 리소스 정리
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _refreshSemaphore?.Dispose();
        _disposed = true;

        _logger.LogInformation("Top300StockService disposed");
    }
}
