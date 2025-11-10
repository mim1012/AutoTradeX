using AutoTrader.Core.Models.Realtime;
using AutoTrader.Core.Models.Trading;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AutoTrader.Core.Services.Trading;

/// <summary>
/// 매수 후보 종목 추적 서비스 구현
/// - 2회 연속 조건 확인 (10초 간격)
/// - Thread-safe 후보 관리
/// - 만료된 후보 자동 제거
/// </summary>
public class CandidateTracker : ICandidateTracker
{
    private readonly ILogger<CandidateTracker> _logger;

    // Thread-safe 후보 목록 (Key: Symbol)
    private readonly ConcurrentDictionary<string, CandidateStock> _candidates = new();

    public CandidateTracker(ILogger<CandidateTracker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public void TrackCandidate(CachedStockData stockData)
    {
        if (stockData == null || string.IsNullOrEmpty(stockData.Symbol))
            return;

        _candidates.AddOrUpdate(
            stockData.Symbol,
            // 새 후보 추가 (첫 번째 확인)
            symbol => CreateNewCandidate(stockData),
            // 기존 후보 업데이트 (두 번째 확인)
            (symbol, existing) => UpdateExistingCandidate(existing, stockData));
    }

    /// <inheritdoc/>
    public void TrackCandidates(List<CachedStockData> stockDataList)
    {
        if (stockDataList == null || stockDataList.Count == 0)
            return;

        foreach (var stockData in stockDataList)
        {
            TrackCandidate(stockData);
        }

        _logger.LogDebug("Tracked {Count} candidates", stockDataList.Count);
    }

    /// <inheritdoc/>
    public List<CandidateStock> GetConfirmedCandidates()
    {
        return _candidates.Values
            .Where(c => c.IsFullyConfirmed)
            .OrderBy(c => c.CurrentChangeRate) // 등락률 낮은 순 (하락률 높은 순)
            .ToList();
    }

    /// <inheritdoc/>
    public List<CandidateStock> GetPendingCandidates()
    {
        return _candidates.Values
            .Where(c => !c.IsFullyConfirmed && !c.IsExpired)
            .OrderBy(c => c.CurrentChangeRate)
            .ToList();
    }

    /// <inheritdoc/>
    public List<CandidateStock> GetAllCandidates()
    {
        return _candidates.Values
            .OrderBy(c => c.CurrentChangeRate)
            .ToList();
    }

    /// <inheritdoc/>
    public int RemoveExpiredCandidates()
    {
        var expiredSymbols = _candidates.Values
            .Where(c => !c.IsFullyConfirmed && c.IsExpired)
            .Select(c => c.Symbol)
            .ToList();

        int removedCount = 0;
        foreach (var symbol in expiredSymbols)
        {
            if (_candidates.TryRemove(symbol, out var removed))
            {
                removedCount++;
                _logger.LogDebug("Removed expired candidate: {Symbol} (elapsed: {Seconds}s)",
                    removed.Symbol, removed.SecondsSinceFirstConfirmation);
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("Removed {Count} expired candidates", removedCount);
        }

        return removedCount;
    }

    /// <inheritdoc/>
    public void ClearCandidates()
    {
        var count = _candidates.Count;
        _candidates.Clear();

        _logger.LogInformation("Cleared {Count} candidates", count);
    }

    /// <inheritdoc/>
    public int ConfirmedCandidateCount => _candidates.Values.Count(c => c.IsFullyConfirmed);

    /// <inheritdoc/>
    public int PendingCandidateCount => _candidates.Values.Count(c => !c.IsFullyConfirmed && !c.IsExpired);

    /// <summary>
    /// 새 후보 생성 (첫 번째 확인)
    /// </summary>
    private CandidateStock CreateNewCandidate(CachedStockData stockData)
    {
        var candidate = new CandidateStock
        {
            Symbol = stockData.Symbol,
            Name = stockData.LatestData?.Symbol ?? stockData.Symbol,
            FirstConfirmedAt = DateTime.UtcNow,
            SecondConfirmedAt = null,
            CurrentChangeRate = stockData.ChangeRate,
            CurrentPrice = stockData.CurrentPrice,
            TradeAmount = stockData.TradeAmount
        };

        _logger.LogInformation("New candidate tracked (1st confirmation): {Candidate}",
            candidate);

        return candidate;
    }

    /// <summary>
    /// 기존 후보 업데이트 (두 번째 확인)
    /// </summary>
    private CandidateStock UpdateExistingCandidate(CandidateStock existing, CachedStockData stockData)
    {
        // 이미 2회 확인 완료된 경우 업데이트만
        if (existing.IsFullyConfirmed)
        {
            existing.CurrentChangeRate = stockData.ChangeRate;
            existing.CurrentPrice = stockData.CurrentPrice;
            existing.TradeAmount = stockData.TradeAmount;
            return existing;
        }

        // 첫 확인 후 10초 이상 경과 → 두 번째 확인 완료
        if (existing.SecondsSinceFirstConfirmation >= 10)
        {
            existing.SecondConfirmedAt = DateTime.UtcNow;
            existing.CurrentChangeRate = stockData.ChangeRate;
            existing.CurrentPrice = stockData.CurrentPrice;
            existing.TradeAmount = stockData.TradeAmount;

            _logger.LogInformation("Candidate confirmed (2nd confirmation): {Candidate}",
                existing);
        }
        else
        {
            // 10초 미만이면 데이터만 업데이트
            existing.CurrentChangeRate = stockData.ChangeRate;
            existing.CurrentPrice = stockData.CurrentPrice;
            existing.TradeAmount = stockData.TradeAmount;
        }

        return existing;
    }
}
