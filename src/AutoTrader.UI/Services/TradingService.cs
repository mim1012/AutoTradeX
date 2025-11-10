using AutoTrader.UI.Models;

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
        
        // 샘플 데이터
        _top300Stocks.Add(new StockInfo
        {
            Rank = 1,
            Symbol = "TSLA",
            Name = "Tesla Inc",
            CurrentPrice = 245.50m,
            ChangeRate = -3.25m,
            TradeAmount = 15000000000m,
            IsConditionMet = true
        });

        _top300Stocks.Add(new StockInfo
        {
            Rank = 2,
            Symbol = "AAPL",
            Name = "Apple Inc",
            CurrentPrice = 185.20m,
            ChangeRate = 1.15m,
            TradeAmount = 12000000000m,
            IsConditionMet = false
        });

        _top300Stocks.Add(new StockInfo
        {
            Rank = 3,
            Symbol = "NVDA",
            Name = "NVIDIA Corp",
            CurrentPrice = 520.30m,
            ChangeRate = -5.80m,
            TradeAmount = 10000000000m,
            IsConditionMet = true,
            IsCandidate = true
        });

        LogReceived?.Invoke(this, $"[SUCCESS] {_top300Stocks.Count}개 종목 조회 완료");
        StocksUpdated?.Invoke(this, EventArgs.Empty);

        return _top300Stocks;
    }

    public List<StockInfo> GetCandidateStocks()
    {
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
