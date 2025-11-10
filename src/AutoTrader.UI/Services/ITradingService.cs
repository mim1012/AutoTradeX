using AutoTrader.UI.Models;

namespace AutoTrader.UI.Services;

/// <summary>
/// UI와 백엔드 Trading 로직을 연결하는 서비스 인터페이스
/// </summary>
public interface ITradingService
{
    /// <summary>
    /// 시스템 시작
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 시스템 중지
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 거래대금 상위 300 종목 조회
    /// </summary>
    Task<List<StockInfo>> GetTop300StocksAsync();

    /// <summary>
    /// 조건 충족 후보 종목 조회
    /// </summary>
    List<StockInfo> GetCandidateStocks();

    /// <summary>
    /// 주문 예정 종목 조회
    /// </summary>
    List<StockInfo> GetOrderPendingStocks();

    /// <summary>
    /// API 접속 상태 확인
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 계좌 정보 조회
    /// </summary>
    Task<(decimal balance, decimal totalAssets)> GetAccountInfoAsync();

    /// <summary>
    /// 시장 상태 조회
    /// </summary>
    (string status, string closeTime, bool isDst) GetMarketStatus();

    /// <summary>
    /// 장마감까지 남은 시간
    /// </summary>
    TimeSpan GetTimeUntilClose();

    /// <summary>
    /// 로그 이벤트
    /// </summary>
    event EventHandler<string>? LogReceived;

    /// <summary>
    /// 종목 데이터 업데이트 이벤트
    /// </summary>
    event EventHandler? StocksUpdated;
}
