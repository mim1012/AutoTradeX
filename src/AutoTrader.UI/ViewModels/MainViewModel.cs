using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Repositories;
using AutoTrader.UI.Commands;
using AutoTrader.UI.Models;
using AutoTrader.UI.Services;
using AutoTrader.UI.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;

namespace AutoTrader.UI.ViewModels;

/// <summary>
/// 메인 윈도우의 ViewModel
/// </summary>
public class MainViewModel : ViewModelBase
{
    #region Fields

    private readonly ITradingService _tradingService;
    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _workerMonitorTimer;
    private readonly AccountRepository? _accountRepository;
    private readonly ConditionSetRepository? _conditionSetRepository;
    private readonly WorkerStatusRepository? _workerStatusRepository;
    private Account? _activeAccount;
    private ConditionSet? _activeConditionSet;
    private bool _isSystemRunning;
    private string _workerStatus = "중지됨";
    private string _workerLastHeartbeat = "--:--";
    private int _workerTop300Count;
    private int _workerCandidateCount;
    private int _workerOrderCount;
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
    /// Worker 서비스 상태
    /// </summary>
    public string WorkerStatus
    {
        get => _workerStatus;
        set => SetProperty(ref _workerStatus, value);
    }

    /// <summary>
    /// Worker 마지막 하트비트 시간
    /// </summary>
    public string WorkerLastHeartbeat
    {
        get => _workerLastHeartbeat;
        set => SetProperty(ref _workerLastHeartbeat, value);
    }

    /// <summary>
    /// Worker Top300 종목 수
    /// </summary>
    public int WorkerTop300Count
    {
        get => _workerTop300Count;
        set => SetProperty(ref _workerTop300Count, value);
    }

    /// <summary>
    /// Worker 후보 종목 수
    /// </summary>
    public int WorkerCandidateCount
    {
        get => _workerCandidateCount;
        set => SetProperty(ref _workerCandidateCount, value);
    }

    /// <summary>
    /// Worker 주문 종목 수
    /// </summary>
    public int WorkerOrderCount
    {
        get => _workerOrderCount;
        set => SetProperty(ref _workerOrderCount, value);
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

    /// <summary>
    /// 조건식 빌더 ViewModel
    /// </summary>
    public ConditionBuilderViewModel ConditionBuilder { get; }

    /// <summary>
    /// 활성 계좌 표시 문자열
    /// </summary>
    public string ActiveAccountDisplay =>
        _activeAccount != null
            ? $"{_activeAccount.AccountNumber} ({_activeAccount.AccountName})"
            : "활성 계좌 없음";

    /// <summary>
    /// 조건식 표시 문자열
    /// </summary>
    public string ConditionSetDisplay
    {
        get
        {
            if (_activeConditionSet == null || _activeConditionSet.Conditions.Count == 0)
                return "조건식 미설정";
            return $"조건 {_activeConditionSet.Conditions.Count}개";
        }
    }

    /// <summary>
    /// 시스템 상태 텍스트
    /// </summary>
    public string SystemStatusText =>
        _isSystemRunning ? "🟢 실행 중" : "🔴 중지됨";

    /// <summary>
    /// 시작/중지 버튼 텍스트
    /// </summary>
    public string StartStopButtonText =>
        _isSystemRunning ? "⏸️ 자동매매 중지" : "▶️ 자동매매 시작";

    /// <summary>
    /// 시스템 상태 색상
    /// </summary>
    public Brush SystemStatusColor =>
        _isSystemRunning ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : new SolidColorBrush(Color.FromRgb(244, 67, 54));

    /// <summary>
    /// 시작/중지 버튼 배경색
    /// </summary>
    public Brush StartStopButtonBackground =>
        _isSystemRunning ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) : new SolidColorBrush(Color.FromRgb(76, 175, 80));

    /// <summary>
    /// 마지막 업데이트 시간
    /// </summary>
    public string LastUpdateTime => $"마지막 업데이트: {DateTime.Now:HH:mm:ss}";

    /// <summary>
    /// Top 300 종목 수
    /// </summary>
    public string Top300StocksCount => $"({Top300Stocks.Count}개)";

    /// <summary>
    /// 조건 충족 종목 수
    /// </summary>
    public string MatchedStocksCount => $"({CandidateStocks.Count}개)";

    /// <summary>
    /// 조건 충족 종목 (CandidateStocks의 별칭)
    /// </summary>
    public ObservableCollection<StockInfo> MatchedStocks => CandidateStocks;

    /// <summary>
    /// 시스템 로그 (LogText의 별칭)
    /// </summary>
    public string SystemLogs => LogText;

    #endregion

    #region Commands

    public ICommand StartSystemCommand { get; }
    public ICommand StopSystemCommand { get; }
    public ICommand RefreshTop300Command { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand OpenAccountManagementCommand { get; }
    public ICommand OpenConditionEditorCommand { get; }
    public ICommand ToggleSystemCommand { get; }
    public ICommand SaveLogsCommand { get; }
    public ICommand ClearLogsCommand { get; }

    #endregion

    #region Constructor

    public MainViewModel() : this(new TradingService())
    {
    }

    public MainViewModel(
        ITradingService tradingService,
        AccountRepository? accountRepository = null,
        ConditionSetRepository? conditionSetRepository = null,
        WorkerStatusRepository? workerStatusRepository = null)
    {
        _tradingService = tradingService;
        _accountRepository = accountRepository;
        _conditionSetRepository = conditionSetRepository;
        _workerStatusRepository = workerStatusRepository;

        // ViewModel 초기화
        ConditionBuilder = new ConditionBuilderViewModel(_accountRepository, _conditionSetRepository);

        // 이벤트 구독
        _tradingService.LogReceived += OnLogReceived;
        _tradingService.StocksUpdated += OnStocksUpdated;

        // Commands 초기화
        StartSystemCommand = new RelayCommand(async _ => await StartSystem());
        StopSystemCommand = new RelayCommand(async _ => await StopSystem());
        RefreshTop300Command = new RelayCommand(async _ => await RefreshTop300());
        ClearLogCommand = new RelayCommand(_ => ClearLog());
        OpenAccountManagementCommand = new RelayCommand(_ => OpenAccountManagement());
        OpenConditionEditorCommand = new RelayCommand(_ => OpenConditionEditor());
        ToggleSystemCommand = new RelayCommand(_ => ToggleSystem());
        SaveLogsCommand = new RelayCommand(_ => SaveLogs());
        ClearLogsCommand = new RelayCommand(_ => ClearLog());

        // 타이머 설정 (1초마다 UI 업데이트)
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += OnTimerTick;
        _updateTimer.Start();

        // Worker 모니터링 타이머 설정 (5초마다 Worker 상태 확인)
        _workerMonitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _workerMonitorTimer.Tick += OnWorkerMonitorTick;
        _workerMonitorTimer.Start();

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
        await LoadActiveAccountAsync();
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

    /// <summary>
    /// Worker 상태 모니터링 (5초마다)
    /// </summary>
    private async void OnWorkerMonitorTick(object? sender, EventArgs e)
    {
        if (_workerStatusRepository == null)
            return;

        try
        {
            var status = await _workerStatusRepository.GetLatestAsync();

            if (status == null)
            {
                WorkerStatus = "중지됨";
                WorkerLastHeartbeat = "--:--";
                WorkerTop300Count = 0;
                WorkerCandidateCount = 0;
                WorkerOrderCount = 0;
                return;
            }

            // 상태 업데이트
            var isAlive = await _workerStatusRepository.IsWorkerAliveAsync();
            WorkerStatus = isAlive ? "실행 중" : "응답 없음";

            // 로컬 시간으로 변환
            var localHeartbeat = status.LastHeartbeat.ToLocalTime();
            WorkerLastHeartbeat = localHeartbeat.ToString("HH:mm:ss");

            // 통계 업데이트
            WorkerTop300Count = status.Top300Count;
            WorkerCandidateCount = status.CandidateCount;
            WorkerOrderCount = status.OrderCount;

            // 로그에 Worker 상태 표시 (응답 없을 때만)
            if (!isAlive && status.IsRunning)
            {
                var secondsSince = await _workerStatusRepository.GetSecondsSinceLastHeartbeatAsync();
                AddLog($"[WARNING] Worker 응답 없음 (마지막 하트비트: {secondsSince}초 전)");
            }
        }
        catch (Exception ex)
        {
            WorkerStatus = "오류";
            AddLog($"[ERROR] Worker 상태 확인 실패: {ex.Message}");
        }
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

    private async Task LoadActiveAccountAsync()
    {
        if (_accountRepository == null) return;

        try
        {
            _activeAccount = await _accountRepository.GetActiveAccountAsync();
            _activeConditionSet = _activeAccount?.ConditionSet;

            OnPropertyChanged(nameof(ActiveAccountDisplay));
            OnPropertyChanged(nameof(ConditionSetDisplay));

            AddLog($"[INFO] 활성 계좌 로드: {ActiveAccountDisplay}");
        }
        catch (Exception ex)
        {
            AddLog($"[ERROR] 활성 계좌 로드 실패: {ex.Message}");
        }
    }

    private void OpenAccountManagement()
    {
        var viewModel = new AccountManagementViewModel();
        var dialog = new AccountManagementDialog(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            _ = LoadActiveAccountAsync();
        }
    }

    private void OpenConditionEditor()
    {
        if (_activeAccount == null)
        {
            MessageBox.Show("활성 계좌를 먼저 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new ConditionSetEditorDialog
        {
            Owner = Application.Current.MainWindow
        };

        // ViewModel에 계좌 ID 전달
        if (dialog.DataContext is ConditionSetEditorViewModel viewModel)
        {
            viewModel.LoadConditionSet(_activeAccount.AccountId);
        }

        dialog.ShowDialog();
        // 조건식 변경 후 다시 로드
        _ = LoadActiveAccountAsync();
    }

    private void ToggleSystem()
    {
        if (_activeAccount == null)
        {
            MessageBox.Show("활성 계좌를 먼저 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_activeConditionSet == null || _activeConditionSet.Conditions.Count == 0)
        {
            MessageBox.Show("조건식을 먼저 설정해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isSystemRunning = !_isSystemRunning;

        OnPropertyChanged(nameof(SystemStatusText));
        OnPropertyChanged(nameof(StartStopButtonText));
        OnPropertyChanged(nameof(SystemStatusColor));
        OnPropertyChanged(nameof(StartStopButtonBackground));

        if (_isSystemRunning)
        {
            AddLog("[INFO] 자동매매 시스템 시작");
            _ = StartSystem();
        }
        else
        {
            AddLog("[INFO] 자동매매 시스템 중지");
            _ = StopSystem();
        }
    }

    private void SaveLogs()
    {
        try
        {
            var fileName = $"AutoTradeX_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
            System.IO.File.WriteAllText(filePath, LogText);
            MessageBox.Show($"로그가 저장되었습니다.\n{filePath}", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            AddLog($"[INFO] 로그 저장됨: {filePath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"로그 저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            AddLog($"[ERROR] 로그 저장 실패: {ex.Message}");
        }
    }

    #endregion
}
