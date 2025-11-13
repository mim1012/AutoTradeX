using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Repositories;
using AutoTrader.UI.Commands;
using AutoTrader.UI.Views;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoTrader.UI.ViewModels
{
    /// <summary>
    /// 메인 대시보드 ViewModel
    /// </summary>
    public class MainDashboardViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;
        private readonly AccountRepository _accountRepository;
        private readonly ConditionSetRepository _conditionSetRepository;

        private Account? _activeAccount;
        private ConditionSet? _activeConditionSet;
        private bool _isSystemRunning;
        private string _systemLogs = string.Empty;

        public MainDashboardViewModel()
        {
            // DbContext 초기화
            var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite("Data Source=autotrader.db");
            _dbContext = new AppDbContext(optionsBuilder.Options);

            // Repository 초기화
            _accountRepository = new AccountRepository(_dbContext);
            _conditionSetRepository = new ConditionSetRepository(_dbContext);

            // Command 초기화
            OpenAccountManagementCommand = new RelayCommand(OpenAccountManagement);
            OpenConditionEditorCommand = new RelayCommand(OpenConditionEditor);
            ToggleSystemCommand = new RelayCommand(ToggleSystem);
            SaveLogsCommand = new RelayCommand(SaveLogs);
            ClearLogsCommand = new RelayCommand(ClearLogs);

            // 초기 데이터 로드
            _ = LoadActiveAccountAsync();
        }

        #region Properties

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

                // 간단한 요약 (예: "조건 3개")
                return $"조건 {_activeConditionSet.Conditions.Count}개";
            }
        }

        /// <summary>
        /// 시스템 상태 색상
        /// </summary>
        public Brush SystemStatusColor =>
            _isSystemRunning ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);

        /// <summary>
        /// 시스템 상태 텍스트
        /// </summary>
        public string SystemStatusText =>
            _isSystemRunning ? "실행 중" : "중지됨";

        /// <summary>
        /// 시작/중지 버튼 텍스트
        /// </summary>
        public string StartStopButtonText =>
            _isSystemRunning ? "⏸️ 자동매매 중지" : "▶️ 자동매매 시작";

        /// <summary>
        /// 시작/중지 버튼 배경색
        /// </summary>
        public Brush StartStopButtonBackground =>
            _isSystemRunning ? new SolidColorBrush(Colors.OrangeRed) : new SolidColorBrush(Color.FromRgb(33, 150, 243));

        /// <summary>
        /// 마지막 업데이트 시간
        /// </summary>
        public string LastUpdateTime => $"마지막 업데이트: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        /// <summary>
        /// 거래대금 상위 300 종목 개수
        /// </summary>
        public string Top300StocksCount => $"({Top300Stocks.Count}개)";

        /// <summary>
        /// 조건 충족 종목 개수
        /// </summary>
        public string MatchedStocksCount => $"({MatchedStocks.Count}개)";

        /// <summary>
        /// 시스템 로그
        /// </summary>
        public string SystemLogs
        {
            get => _systemLogs;
            set => SetProperty(ref _systemLogs, value);
        }

        /// <summary>
        /// 거래대금 상위 300 종목 (임시 데이터)
        /// </summary>
        public ObservableCollection<StockItem> Top300Stocks { get; } = new ObservableCollection<StockItem>();

        /// <summary>
        /// 조건 충족 종목 (임시 데이터)
        /// </summary>
        public ObservableCollection<StockItem> MatchedStocks { get; } = new ObservableCollection<StockItem>();

        #endregion

        #region Commands

        public ICommand OpenAccountManagementCommand { get; }
        public ICommand OpenConditionEditorCommand { get; }
        public ICommand ToggleSystemCommand { get; }
        public ICommand SaveLogsCommand { get; }
        public ICommand ClearLogsCommand { get; }

        #endregion

        #region Methods

        /// <summary>
        /// 활성 계좌 로드
        /// </summary>
        private async Task LoadActiveAccountAsync()
        {
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

        /// <summary>
        /// 계좌 관리 모달 열기
        /// </summary>
        private void OpenAccountManagement()
        {
            var dialog = new AccountManagementDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                // 계좌 변경 후 다시 로드
                _ = LoadActiveAccountAsync();
            }
        }

        /// <summary>
        /// 조건식 편집 모달 열기
        /// </summary>
        private void OpenConditionEditor()
        {
            if (_activeAccount == null)
            {
                MessageBox.Show("활성 계좌를 먼저 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new ConditionBuilderDialog
            {
                Owner = Application.Current.MainWindow
            };

            dialog.ShowDialog();
            // 조건식 변경 후 다시 로드
            _ = LoadActiveAccountAsync();
        }

        /// <summary>
        /// 시스템 시작/중지 토글
        /// </summary>
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

            OnPropertyChanged(nameof(SystemStatusColor));
            OnPropertyChanged(nameof(SystemStatusText));
            OnPropertyChanged(nameof(StartStopButtonText));
            OnPropertyChanged(nameof(StartStopButtonBackground));

            if (_isSystemRunning)
            {
                AddLog("[INFO] 자동매매 시스템 시작");
                // TODO: 실제 자동매매 로직 시작
            }
            else
            {
                AddLog("[INFO] 자동매매 시스템 중지");
                // TODO: 실제 자동매매 로직 중지
            }
        }

        /// <summary>
        /// 로그 저장
        /// </summary>
        private void SaveLogs()
        {
            try
            {
                var fileName = $"autotrader_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                System.IO.File.WriteAllText(fileName, SystemLogs);
                AddLog($"[INFO] 로그 저장 완료: {fileName}");
                MessageBox.Show($"로그가 저장되었습니다.\n{fileName}", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"[ERROR] 로그 저장 실패: {ex.Message}");
                MessageBox.Show($"로그 저장에 실패했습니다.\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 로그 지우기
        /// </summary>
        private void ClearLogs()
        {
            SystemLogs = string.Empty;
            AddLog("[INFO] 로그 초기화");
        }

        /// <summary>
        /// 로그 추가
        /// </summary>
        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SystemLogs += $"{timestamp} {message}\n";
            OnPropertyChanged(nameof(SystemLogs));
        }

        #endregion
    }

    /// <summary>
    /// 종목 정보 (임시 모델)
    /// </summary>
    public class StockItem
    {
        public int Rank { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public double ChangeRate { get; set; }
        public decimal TradeAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ConfirmCount { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
    }
}
