using AutoTrader.UI.Models;
using System.Linq;

namespace AutoTrader.UI.Services;

/// <summary>
/// UI와 백엔드 Trading 로직을 연결하는 서비스 구현
/// TODO: 실제 AutoTrader.Core 서비스와 연결
/// </summary>
public class TradingService : ITradingService
{
    private bool _isConnected;
    private readonly List<StockInfo> _top300Stocks = new();
    private readonly List<StockInfo> _candidateStocks = new();
    private readonly List<StockInfo> _orderPendingStocks = new();

    public bool IsConnected => _isConnected;

    public event EventHandler<string>? LogReceived;
    public event EventHandler? StocksUpdated;

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

        // TODO: 실제 KIS API 호출
        await Task.Delay(1000);

        _top300Stocks.Clear();

        // 샘플 데이터 300개 생성
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

        LogReceived?.Invoke(this, $"[SUCCESS] {_top300Stocks.Count}개 종목 조회 완료");
        StocksUpdated?.Invoke(this, EventArgs.Empty);

        return _top300Stocks;
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
        // TODO: 실제 계좌 조회 API 호출
        await Task.Delay(500);
        return (50000m, 100000m);
    }

    public (string status, string closeTime, bool isDst) GetMarketStatus()
    {
        // TODO: 실제 시장 상태 조회
        var now = DateTime.Now;
        var hour = now.Hour;

        // 미국 동부시간 기준 (간단한 시뮬레이션)
        if (hour >= 9 && hour < 16)
        {
            return ("개장", "16:00 EST", false);
        }
        else
        {
            return ("폐장", "16:00 EST", false);
        }
    }

    public TimeSpan GetTimeUntilClose()
    {
        // TODO: 실제 마감 시간 계산
        var now = DateTime.Now;
        var closeTime = new DateTime(now.Year, now.Month, now.Day, 16, 0, 0);
        
        if (now > closeTime)
        {
            closeTime = closeTime.AddDays(1);
        }

        return closeTime - now;
    }
}
