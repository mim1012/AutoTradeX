using AutoTrader.Core.DTOs.WebSocket;
using AutoTrader.Core.Models.Realtime;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AutoTrader.Core.Services.Realtime;

/// <summary>
/// 실시간 데이터 집계 서비스 구현
/// - ConcurrentDictionary 기반 Thread-safe 캐싱
/// - 종목별 최신 데이터 및 통계 관리
/// - 데이터 신선도 체크 (10초)
/// - 자동 메모리 정리 (10분마다)
/// </summary>
public class RealtimeDataAggregator : IRealtimeDataAggregator, IDisposable
{
    private readonly ILogger<RealtimeDataAggregator> _logger;

    // Thread-safe 캐시 (Key: Symbol)
    private readonly ConcurrentDictionary<string, CachedStockData> _dataCache = new();

    // 통계
    private long _totalUpdates;

    // 자동 메모리 정리 타이머 (10분마다 실행)
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public RealtimeDataAggregator(ILogger<RealtimeDataAggregator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 10분마다 자동으로 만료된 데이터 제거
        _cleanupTimer = new Timer(
            callback: _ => RemoveStaleData(),
            state: null,
            dueTime: TimeSpan.FromMinutes(10),
            period: TimeSpan.FromMinutes(10));

        _logger.LogInformation("RealtimeDataAggregator initialized with automatic cleanup every 10 minutes");
    }

    /// <inheritdoc/>
    public void UpdateData(RealtimeStockData data)
    {
        if (data == null || string.IsNullOrEmpty(data.Symbol))
        {
            _logger.LogWarning("Received invalid realtime data (null or empty symbol)");
            return;
        }

        _dataCache.AddOrUpdate(
            data.Symbol,
            // 새 데이터 추가
            symbol => new CachedStockData
            {
                Symbol = symbol,
                LatestData = data,
                LastUpdatedAt = DateTime.UtcNow,
                UpdateCount = 1
            },
            // 기존 데이터 업데이트
            (symbol, existing) =>
            {
                existing.LatestData = data;
                existing.LastUpdatedAt = DateTime.UtcNow;
                existing.UpdateCount++;
                return existing;
            });

        Interlocked.Increment(ref _totalUpdates);

        // 100번마다 로깅 (성능 고려)
        if (_totalUpdates % 100 == 0)
        {
            _logger.LogDebug("Realtime data updated: {Symbol} @ {Price} ({Rate}%) [Total: {Count}]",
                data.Symbol, data.CurrentPrice, data.ChangeRate, _totalUpdates);
        }
    }

    /// <inheritdoc/>
    public CachedStockData? GetLatestData(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
            return null;

        return _dataCache.TryGetValue(symbol, out var data) ? data : null;
    }

    /// <inheritdoc/>
    public List<CachedStockData> GetAllData()
    {
        return _dataCache.Values.ToList();
    }

    /// <inheritdoc/>
    public List<CachedStockData> GetFreshData()
    {
        return _dataCache.Values
            .Where(data => data.IsFresh)
            .ToList();
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        _dataCache.Clear();
        _totalUpdates = 0;

        _logger.LogInformation("Realtime data cache cleared");
    }

    /// <inheritdoc/>
    public int CachedStockCount => _dataCache.Count;

    /// <inheritdoc/>
    public int FreshStockCount => _dataCache.Values.Count(data => data.IsFresh);

    /// <inheritdoc/>
    public long TotalUpdates => _totalUpdates;

    /// <summary>
    /// 만료된 데이터 제거 (메모리 최적화)
    /// </summary>
    /// <param name="maxAge">최대 보관 시간 (기본: 1분)</param>
    /// <returns>제거된 항목 수</returns>
    public int RemoveStaleData(TimeSpan? maxAge = null)
    {
        var threshold = maxAge ?? TimeSpan.FromMinutes(1);

        var staleSymbols = _dataCache.Values
            .Where(data => DateTime.UtcNow - data.LastUpdatedAt > threshold)
            .Select(data => data.Symbol)
            .ToList();

        int removedCount = 0;
        foreach (var symbol in staleSymbols)
        {
            if (_dataCache.TryRemove(symbol, out _))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("Removed {Count} stale stock data entries (older than {Threshold})",
                removedCount, threshold);
        }

        return removedCount;
    }

    /// <summary>
    /// Dispose 패턴 구현 - Timer 리소스 정리
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _cleanupTimer?.Dispose();
        _disposed = true;

        _logger.LogInformation("RealtimeDataAggregator disposed");
    }
}
