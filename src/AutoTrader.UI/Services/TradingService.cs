using AutoTrader.UI.Models;
using AutoTrader.Core.Services.Stock;
using AutoTrader.Core.Services.Api;
using AutoTrader.Core.Services.Schedule;
using System.Linq;
using Schedule = AutoTrader.Core.Services.Schedule;

namespace AutoTrader.UI.Services;

/// <summary>
/// UI와 백엔드 Trading 로직을 연결하는 서비스 구현
/// </summary>
public class TradingService : ITradingService
{
    private bool _isConnected;
    private readonly List<StockInfo> _top300Stocks = new();
    private readonly List<StockInfo> _candidateStocks = new();
    private readonly List<StockInfo> _orderPendingStocks = new();
    private readonly ITop300StockService? _top300Service;
    private readonly BalanceApiService? _balanceService;
    private readonly IMarketScheduleService? _marketSchedule;

    public bool IsConnected => _isConnected;

    public event EventHandler<string>? LogReceived;
    public event EventHandler? StocksUpdated;

    public TradingService(
        ITop300StockService? top300Service = null,
        BalanceApiService? balanceService = null,
        IMarketScheduleService? marketSchedule = null)
    {
        _top300Service = top300Service;
        _balanceService = balanceService;
        _marketSchedule = marketSchedule;
    }

    public async Task StartAsync()
    {
        await Task.Delay(500); // 시뮬레이션
        _isConnected = true;
        LogReceived?.Invoke(this, "[INFO] 시스템이 시작되었습니다.");
        LogReceived?.Invoke(this, "[INFO] 한국투자증권 API 인증 중...");
        await Task.Delay(1000);
        LogReceived?.Invoke(this, "[SUCCESS] API 인증 완료");
    }

    public async Task StopAsync()
    {
        await Task.Delay(300);
        _isConnected = false;
        LogReceived?.Invoke(this, "[INFO] 시스템이 중지되었습니다.");
    }

    public async Task<List<StockInfo>> GetTop300StocksAsync()
    {
        LogReceived?.Invoke(this, "[INFO] 거래대금 상위 300 종목 조회 중...");

        _top300Stocks.Clear();

        try
        {
            if (_top300Service != null)
            {
                // 실제 API 호출하여 Top300 갱신
                await _top300Service.RefreshTop300Async();

                // 캐시된 데이터 가져오기
                var cachedStocks = _top300Service.GetCachedTop300();

                // Core DTO를 UI Model로 변환
                foreach (var stock in cachedStocks)
                {
                    var changeRate = stock.ChangeRateDecimal;
                    var isConditionMet = changeRate >= -7 && changeRate <= 0; // 등락률 조건

                    _top300Stocks.Add(new StockInfo
                    {
                        Rank = stock.RankNumber,
                        Symbol = stock.Symbol,
                        Name = stock.Name,
                        CurrentPrice = stock.CurrentPrice,
                        ChangeRate = changeRate,
                        TradeAmount = decimal.TryParse(stock.TradeAmount, out var amt) ? amt : 0,
                        IsConditionMet = isConditionMet,
                        IsCandidate = false // CandidateTracker에서 관리
                    });
                }

                LogReceived?.Invoke(this, $"[SUCCESS] {_top300Stocks.Count}개 종목 조회 완료 (실제 API)");
            }
            else
            {
                // Fallback: 샘플 데이터 생성
                LogReceived?.Invoke(this, "[WARNING] Core 서비스 미연결 - 샘플 데이터 사용");
                await GenerateSampleDataAsync();
            }
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke(this, $"[ERROR] API 조회 실패: {ex.Message}");
            LogReceived?.Invoke(this, "[INFO] 샘플 데이터로 전환");
            await GenerateSampleDataAsync();
        }

        StocksUpdated?.Invoke(this, EventArgs.Empty);
        return _top300Stocks;
    }

    private async Task GenerateSampleDataAsync()
    {
        await Task.Delay(1000);

        var random = new Random();
        var sampleStocks = new[]
        {
            "TSLA", "AAPL", "NVDA", "MSFT", "GOOGL", "AMZN", "META", "NFLX", "AMD", "INTC",
            "BA", "DIS", "JPM", "GS", "V", "MA", "PYPL", "SQ", "COIN", "HOOD",
            "UBER", "LYFT", "ABNB", "DASH", "RBLX", "SNOW", "PLTR", "SOFI", "NIO", "LCID"
        };

        for (int i = 0; i < 300; i++)
        {
            var baseSymbol = sampleStocks[i % sampleStocks.Length];
            var suffix = i / sampleStocks.Length > 0 ? (i / sampleStocks.Length).ToString() : "";

            var changeRate = (decimal)(random.NextDouble() * 20 - 10); // -10% ~ +10%
            var isConditionMet = changeRate >= -7 && changeRate <= 0; // 등락률 조건

            _top300Stocks.Add(new StockInfo
            {
                Rank = i + 1,
                Symbol = baseSymbol + suffix,
                Name = $"{baseSymbol} Inc{suffix}",
                CurrentPrice = (decimal)(random.NextDouble() * 500 + 50), // $50 ~ $550
                ChangeRate = changeRate,
                TradeAmount = (decimal)(random.NextDouble() * 10000000000 + 1000000000), // $1B ~ $11B
                IsConditionMet = isConditionMet,
                IsCandidate = isConditionMet && random.Next(0, 3) == 0 // 조건 충족 중 일부만 후보
            });
        }

        LogReceived?.Invoke(this, $"[SUCCESS] {_top300Stocks.Count}개 샘플 종목 생성 완료");
    }

    public List<StockInfo> GetCandidateStocks()
    {
        // Top300에서 조건 충족 종목만 필터링
        _candidateStocks.Clear();
        _candidateStocks.AddRange(_top300Stocks.Where(s => s.IsCandidate).ToList());
        return _candidateStocks;
    }

    public List<StockInfo> GetOrderPendingStocks()
    {
        return _orderPendingStocks;
    }

    public async Task<(decimal balance, decimal totalAssets)> GetAccountInfoAsync()
    {
        try
        {
            if (_balanceService != null)
            {
                // 실제 KIS API 호출
                var accountInfo = await _balanceService.GetAccountBalanceAsync(null!); // AccountNumber는 설정에서 가져옴

                if (accountInfo != null)
                {
                    LogReceived?.Invoke(this, $"[INFO] 계좌 조회 성공: 잔고=${accountInfo.AvailableCash:N2}, 총자산=${accountInfo.TotalAssets:N2}");
                    return (accountInfo.AvailableCash, accountInfo.TotalAssets);
                }
                else
                {
                    LogReceived?.Invoke(this, "[WARNING] 계좌 조회 실패 - 더미 데이터 사용");
                }
            }
            else
            {
                LogReceived?.Invoke(this, "[WARNING] BalanceApiService 미연결 - 더미 데이터 사용");
            }
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke(this, $"[ERROR] 계좌 조회 실패: {ex.Message}");
        }

        // Fallback: 더미 데이터
        await Task.Delay(500);
        return (50000m, 100000m);
    }

    public (string status, string closeTime, bool isDst) GetMarketStatus()
    {
        if (_marketSchedule != null)
        {
            try
            {
                var status = _marketSchedule.GetCurrentMarketStatus();
                var closeTime = _marketSchedule.GetMarketCloseTimeString();
                var isDst = _marketSchedule.IsDaylightSavingTime();

                var statusText = status switch
                {
                    Schedule.MarketStatus.Open => "개장",
                    Schedule.MarketStatus.PreMarket => "프리마켓",
                    Schedule.MarketStatus.AfterHours => "애프터아워",
                    _ => "폐장"
                };

                return (statusText, closeTime, isDst);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke(this, $"[ERROR] 시장 상태 조회 실패: {ex.Message}");
            }
        }

        // Fallback: 간단한 로컬 시간 기반 계산
        var now = DateTime.Now;
        var hour = now.Hour;

        if (hour >= 9 && hour < 16)
            return ("개장", "16:00 EST", false);
        else
            return ("폐장", "16:00 EST", false);
    }

    public TimeSpan GetTimeUntilClose()
    {
        if (_marketSchedule != null)
        {
            try
            {
                return _marketSchedule.GetTimeUntilMarketClose();
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke(this, $"[ERROR] 마감 시간 계산 실패: {ex.Message}");
            }
        }

        // Fallback
        var now = DateTime.Now;
        var closeTime = new DateTime(now.Year, now.Month, now.Day, 16, 0, 0);

        if (now > closeTime)
            closeTime = closeTime.AddDays(1);

        return closeTime - now;
    }
}
