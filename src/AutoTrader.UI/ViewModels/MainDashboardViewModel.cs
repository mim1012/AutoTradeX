using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Services.Market;
using AutoTrader.Core.Services.Security;
using AutoTrader.Core.Services.Trading;
using AutoTrader.UI.Commands;
using AutoTrader.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        
        // Services
        private readonly KisMarketDataService _marketDataService;
        private readonly TradingEngine _tradingEngine;

        private Account? _activeAccount;
        private ConditionSet? _activeConditionSet;
        private string _systemLogs = string.Empty;

        public MainDashboardViewModel()
        {
            // Configuration 로드 (실행 파일 위치 기준)
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // DbContext 초기화
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var databaseType = configuration.GetValue<string>("ConnectionStrings:DatabaseType") ?? "SQLite";

            switch (databaseType.ToUpper())
            {
                case "SQLITE":
                    optionsBuilder.UseSqlite("Data Source=autotrader.db");
                    break;
                case "MYSQL":
                    var mysqlConnectionString = configuration.GetConnectionString("DefaultConnection");
                    var serverVersion = ServerVersion.AutoDetect(mysqlConnectionString);
                    optionsBuilder.UseMySql(mysqlConnectionString, serverVersion);
                    break;
                case "POSTGRESQL":
                case "POSTGRES":
                    var postgresConnectionString = configuration.GetConnectionString("DefaultConnection");
                    optionsBuilder.UseNpgsql(postgresConnectionString);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database type: {databaseType}");
            }

            _dbContext = new AppDbContext(optionsBuilder.Options);

            // EncryptionService 초기화
            var encryptionService = new EncryptionService(NullLogger<EncryptionService>.Instance);

            // Repository 초기화
            _accountRepository = new AccountRepository(_dbContext, encryptionService);
            _conditionSetRepository = new ConditionSetRepository(_dbContext);

            // TODO: DI Container를 사용하여 주입받아야 함. 현재는 임시로 직접 생성 (데모용)
            // 실제로는 App.xaml.cs에서 DI 설정 후 주입받아야 함.
            // _marketDataService = ...
            // _tradingEngine = ...
            
            // Command 초기화
            OpenAccountManagementCommand = new RelayCommand(OpenAccountManagement);
            OpenConditionEditorCommand = new RelayCommand(OpenConditionEditor);
            ToggleSystemCommand = new RelayCommand(ToggleSystem);
            SaveLogsCommand = new RelayCommand(SaveLogs);
            ClearLogsCommand = new RelayCommand(ClearLogs);

            // 초기 데이터 로드
            _ = LoadActiveAccountAsync();
            // _ = LoadTop300StocksAsync(); // TradingEngine 시작 시 로드됨
        }

        // DI 생성자 추가 (권장)
        public MainDashboardViewModel(
            KisMarketDataService marketDataService,
            TradingEngine tradingEngine,
            AccountRepository accountRepository,
            ConditionSetRepository conditionSetRepository)
        {
            _marketDataService = marketDataService;
            _tradingEngine = tradingEngine;
            _accountRepository = accountRepository;
            _conditionSetRepository = conditionSetRepository;

            _tradingEngine.StatusChanged += OnEngineStatusChanged;

            OpenAccountManagementCommand = new RelayCommand(OpenAccountManagement);
            OpenConditionEditorCommand = new RelayCommand(OpenConditionEditor);
            ToggleSystemCommand = new RelayCommand(ToggleSystem);
            SaveLogsCommand = new RelayCommand(SaveLogs);
            ClearLogsCommand = new RelayCommand(ClearLogs);

            _ = LoadActiveAccountAsync();
        }

        private void OnEngineStatusChanged(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(SystemStatusColor));
                OnPropertyChanged(nameof(SystemStatusText));
                OnPropertyChanged(nameof(StartStopButtonText));
                OnPropertyChanged(nameof(StartStopButtonBackground));
                AddLog($"[SYSTEM] Status changed to: {status}");
            });
        }

        #region Properties

        public string ActiveAccountDisplay =>
            _activeAccount != null
                ? $"{_activeAccount.AccountNumber} ({_activeAccount.AccountName})"
                : "활성 계좌 없음";

        public string ConditionSetDisplay
        {
            get
            {
                if (_activeConditionSet == null || _activeConditionSet.Conditions.Count == 0)
                    return "조건식 미설정";
                return $"조건 {_activeConditionSet.Conditions.Count}개";
            }
        }

        public Brush SystemStatusColor =>
            _tradingEngine?.IsRunning == true ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);

        public string SystemStatusText =>
            _tradingEngine?.IsRunning == true ? "실행 중" : "중지됨";

        public string StartStopButtonText =>
            _tradingEngine?.IsRunning == true ? "⏸️ 자동매매 중지" : "▶️ 자동매매 시작";

        public Brush StartStopButtonBackground =>
            _tradingEngine?.IsRunning == true ? new SolidColorBrush(Colors.OrangeRed) : new SolidColorBrush(Color.FromRgb(33, 150, 243));

        public string LastUpdateTime => $"마지막 업데이트: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        public string Top300StocksCount => $"({Top300Stocks.Count}개)";

        public string MatchedStocksCount => $"({MatchedStocks.Count}개)";

        public string SystemLogs
        {
            get => _systemLogs;
            set => SetProperty(ref _systemLogs, value);
        }

        public ObservableCollection<StockItem> Top300Stocks { get; } = new ObservableCollection<StockItem>();

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

        private async Task LoadActiveAccountAsync()
        {
            try
            {
                if (_accountRepository != null)
                {
                    _activeAccount = await _accountRepository.GetActiveAccountAsync();
                    _activeConditionSet = _activeAccount?.ConditionSet;

                    OnPropertyChanged(nameof(ActiveAccountDisplay));
                    OnPropertyChanged(nameof(ConditionSetDisplay));

                    AddLog($"[INFO] 활성 계좌 로드: {ActiveAccountDisplay}");
                }
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

            if (dialog.DataContext is ViewModels.ConditionSetEditorViewModel viewModel)
            {
                viewModel.LoadConditionSet(_activeAccount.AccountId);
            }

            dialog.ShowDialog();
            _ = LoadActiveAccountAsync();
        }

        private async void ToggleSystem()
        {
            if (_tradingEngine == null)
            {
                MessageBox.Show("Trading Engine이 초기화되지 않았습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_activeAccount == null)
            {
                MessageBox.Show("활성 계좌를 먼저 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_tradingEngine.IsRunning)
            {
                _tradingEngine.Stop();
            }
            else
            {
                await _tradingEngine.StartAsync();
                // Top 300 로드 후 UI 업데이트 (옵션)
                _ = LoadTop300StocksAsync();
            }
        }

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

        private void ClearLogs()
        {
            SystemLogs = string.Empty;
            AddLog("[INFO] 로그 초기화");
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SystemLogs += $"{timestamp} {message}\n";
            OnPropertyChanged(nameof(SystemLogs));
        }

        private async Task LoadTop300StocksAsync()
        {
            if (_marketDataService == null) return;

            try
            {
                AddLog("[INFO] 거래대금 상위 300 종목 로딩 중...");

                var stocks = await _marketDataService.GetTop300StocksAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Top300Stocks.Clear();
                    foreach (var stock in stocks.Take(100))
                    {
                        Top300Stocks.Add(new StockItem
                        {
                            Rank = stock.Rank,
                            Symbol = stock.Symbol,
                            Name = stock.Name,
                            CurrentPrice = stock.CurrentPrice,
                            ChangeRate = (double)stock.ChangePercent,
                            TradeAmount = stock.TradeAmount,
                            Status = ""
                        });
                    }
                    OnPropertyChanged(nameof(Top300StocksCount));
                });
                
                AddLog($"[INFO] 거래대금 상위 {stocks.Count}개 종목 로드 완료");
            }
            catch (Exception ex)
            {
                AddLog($"[ERROR] 종목 데이터 로드 실패: {ex.Message}");
            }
        }

        #endregion
    }

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
