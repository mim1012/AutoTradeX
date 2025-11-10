using AutoTrader.UI.Commands;
using AutoTrader.UI.Models;
using AutoTrader.UI.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace AutoTrader.UI.ViewModels;

/// <summary>
/// 메인 윈도우의 ViewModel
/// </summary>
public class MainViewModel : ViewModelBase
{
    #region Fields

    private readonly ITradingService _tradingService;
    private readonly DispatcherTimer _updateTimer;
    private string _apiConnectionStatus = "연결 대기 중...";
    private string _accountBalance = "$0.00";
    private string _totalAssets = "$0.00";
    private string _marketStatus = "폐장";
    private string _marketCloseTime = "--:--";
    private bool _isDstActive;
    private int _allocationPercent = 5;
    private bool _preventReEntry;
    private string _countdownText = "--:--";
    private string _logText = string.Empty;

    #endregion

    #region Properties

    /// <summary>
    /// API 접속 상태
    /// </summary>
    public string ApiConnectionStatus
    {
        get => _apiConnectionStatus;
        set => SetProperty(ref _apiConnectionStatus, value);
    }

    /// <summary>
    /// 계좌 잔고
    /// </summary>
    public string AccountBalance
    {
        get => _accountBalance;
        set => SetProperty(ref _accountBalance, value);
    }

    /// <summary>
    /// 총 자산
    /// </summary>
    public string TotalAssets
    {
        get => _totalAssets;
        set => SetProperty(ref _totalAssets, value);
    }

    /// <summary>
    /// 시장 상태 (개장/폐장)
    /// </summary>
    public string MarketStatus
    {
        get => _marketStatus;
        set => SetProperty(ref _marketStatus, value);
    }

    /// <summary>
    /// 시장 마감 시간
    /// </summary>
    public string MarketCloseTime
    {
        get => _marketCloseTime;
        set => SetProperty(ref _marketCloseTime, value);
    }

    /// <summary>
    /// 서머타임 활성화 여부
    /// </summary>
    public bool IsDstActive
    {
        get => _isDstActive;
        set => SetProperty(ref _isDstActive, value);
    }

    /// <summary>
    /// 매수 비율 (%)
    /// </summary>
    public int AllocationPercent
    {
        get => _allocationPercent;
        set => SetProperty(ref _allocationPercent, value);
    }

    /// <summary>
    /// 보유 종목 재진입 방지
    /// </summary>
    public bool PreventReEntry
    {
        get => _preventReEntry;
        set => SetProperty(ref _preventReEntry, value);
    }

    /// <summary>
    /// 장마감 카운트다운
    /// </summary>
    public string CountdownText
    {
        get => _countdownText;
        set => SetProperty(ref _countdownText, value);
    }

    /// <summary>
    /// 로그 텍스트
    /// </summary>
    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    /// <summary>
    /// 거래대금 상위 300 종목 리스트
    /// </summary>
    public ObservableCollection<StockInfo> Top300Stocks { get; } = new();

    /// <summary>
    /// 조건 충족 후보 종목 리스트
    /// </summary>
    public ObservableCollection<StockInfo> CandidateStocks { get; } = new();

    /// <summary>
    /// 주문 예정 종목 리스트
    /// </summary>
    public ObservableCollection<StockInfo> OrderPendingStocks { get; } = new();

    #endregion

    #region Commands

    public ICommand StartSystemCommand { get; }
    public ICommand StopSystemCommand { get; }
    public ICommand RefreshTop300Command { get; }
    public ICommand ClearLogCommand { get; }

    #endregion

    #region Constructor

    public MainViewModel() : this(new TradingService())
    {
    }

    public MainViewModel(ITradingService tradingService)
    {
        _tradingService = tradingService;

        // 이벤트 구독
        _tradingService.LogReceived += OnLogReceived;
        _tradingService.StocksUpdated += OnStocksUpdated;

        // Commands 초기화
        StartSystemCommand = new RelayCommand(async _ => await StartSystem());
        StopSystemCommand = new RelayCommand(async _ => await StopSystem());
        RefreshTop300Command = new RelayCommand(async _ => await RefreshTop300());
        ClearLogCommand = new RelayCommand(_ => ClearLog());

        // 타이머 설정 (1초마다 UI 업데이트)
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += OnTimerTick;
        _updateTimer.Start();

        // 초기 데이터 로드
        _ = InitializeAsync();
    }

    #endregion

    #region Methods

    private async Task InitializeAsync()
    {
        AddLog("[INFO] AutoTradeX 시스템 초기화 중...");
        UpdateMarketStatus();
        await UpdateAccountInfo();
        AddLog("[INFO] 초기화 완료. 시스템 시작 버튼을 눌러주세요.");
    }

    private async Task StartSystem()
    {
        AddLog("[INFO] 시스템 시작 요청...");
        await _tradingService.StartAsync();
        
        if (_tradingService.IsConnected)
        {
            ApiConnectionStatus = "✅ 연결됨 (모의투자)";
            await RefreshTop300();
        }
    }

    private async Task StopSystem()
    {
        AddLog("[INFO] 시스템 중지 요청...");
        await _tradingService.StopAsync();
        ApiConnectionStatus = "⛔ 연결 끊김";
    }

    private async Task RefreshTop300()
    {
        var stocks = await _tradingService.GetTop300StocksAsync();
        
        Top300Stocks.Clear();
        foreach (var stock in stocks)
        {
            Top300Stocks.Add(stock);
        }

        UpdateCandidates();
    }

    private void UpdateCandidates()
    {
        CandidateStocks.Clear();
        var candidates = _tradingService.GetCandidateStocks();
        
        foreach (var candidate in candidates)
        {
            CandidateStocks.Add(candidate);
        }

        // 주문 예정 종목 업데이트 (하락률 상위 2개)
        OrderPendingStocks.Clear();
        var orderPending = _tradingService.GetOrderPendingStocks();
        
        foreach (var stock in orderPending)
        {
            OrderPendingStocks.Add(stock);
        }
    }

    private async Task UpdateAccountInfo()
    {
        var (balance, totalAssets) = await _tradingService.GetAccountInfoAsync();
        AccountBalance = $"${balance:N2}";
        TotalAssets = $"${totalAssets:N2}";
    }

    private void UpdateMarketStatus()
    {
        var (status, closeTime, isDst) = _tradingService.GetMarketStatus();
        MarketStatus = status;
        MarketCloseTime = closeTime;
        IsDstActive = isDst;
    }

    private void UpdateCountdown()
    {
        var timeUntilClose = _tradingService.GetTimeUntilClose();
        
        if (timeUntilClose.TotalMinutes <= 10 && timeUntilClose.TotalMinutes > 0)
        {
            CountdownText = $"{timeUntilClose:mm\\:ss}";
        }
        else
        {
            CountdownText = "--:--";
        }
    }

    private void ClearLog()
    {
        LogText = string.Empty;
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        LogText += $"[{timestamp}] {message}\n";
    }

    private void OnLogReceived(object? sender, string message)
    {
        AddLog(message);
    }

    private void OnStocksUpdated(object? sender, EventArgs e)
    {
        UpdateCandidates();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateCountdown();
        UpdateMarketStatus();
    }

    #endregion
}
