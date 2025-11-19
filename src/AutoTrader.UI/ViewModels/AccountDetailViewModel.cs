using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Services.Auth;
using AutoTrader.Core.Services.Api;
using AutoTrader.Core.Services.Security;
using AutoTrader.Core.Configuration;
using AutoTrader.UI.Commands;
using AutoTrader.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoTrader.UI.ViewModels
{
    /// <summary>
    /// 계좌 상세 정보 모달 ViewModel
    /// </summary>
    public class AccountDetailViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;
        private readonly AccountRepository _accountRepository;
        private readonly int? _accountId; // null이면 신규, 값이 있으면 편집

        private string _accountNumber = string.Empty;
        private string _accountName = string.Empty;
        private string _appKey = string.Empty;
        private string _appSecret = string.Empty;
        private bool _excludeOwnedStocks = false;
        private string _apiTestStatus = string.Empty;
        private Brush _apiTestStatusColor = new SolidColorBrush(Colors.Black);

        public AccountDetailViewModel() : this(null)
        {
        }

        public AccountDetailViewModel(int? accountId)
        {
            _accountId = accountId;

            // Configuration 로드 (실행 파일 위치 기준)
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // DbContext 초기화 (appsettings.json에서 데이터베이스 타입 읽기)
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

            // Command 초기화
            ToggleAppKeyVisibilityCommand = new RelayCommand(ToggleAppKeyVisibility);
            ToggleAppSecretVisibilityCommand = new RelayCommand(ToggleAppSecretVisibility);
            TestApiConnectionCommand = new RelayCommand(TestApiConnection);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);

            // 편집 모드면 기존 데이터 로드
            if (_accountId.HasValue)
            {
                _ = LoadAccountAsync(_accountId.Value);
            }
        }

        #region Properties

        public string AccountNumber
        {
            get => _accountNumber;
            set => SetProperty(ref _accountNumber, value);
        }

        public string AccountName
        {
            get => _accountName;
            set => SetProperty(ref _accountName, value);
        }

        public string AppKey
        {
            get => _appKey;
            set => SetProperty(ref _appKey, value);
        }

        public string AppSecret
        {
            get => _appSecret;
            set => SetProperty(ref _appSecret, value);
        }

        public bool ExcludeOwnedStocks
        {
            get => _excludeOwnedStocks;
            set => SetProperty(ref _excludeOwnedStocks, value);
        }

        public string ApiTestStatus
        {
            get => _apiTestStatus;
            set => SetProperty(ref _apiTestStatus, value);
        }

        public Brush ApiTestStatusColor
        {
            get => _apiTestStatusColor;
            set => SetProperty(ref _apiTestStatusColor, value);
        }

        #endregion

        #region Commands

        public ICommand ToggleAppKeyVisibilityCommand { get; }
        public ICommand ToggleAppSecretVisibilityCommand { get; }
        public ICommand TestApiConnectionCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Methods

        /// <summary>
        /// 기존 계좌 데이터 로드
        /// </summary>
        private async Task LoadAccountAsync(int accountId)
        {
            try
            {
                var account = await _accountRepository.GetAccountByIdAsync(accountId);
                if (account != null)
                {
                    AccountNumber = account.AccountNumber;
                    AccountName = account.AccountName ?? string.Empty;
                    AppKey = account.AppKey;
                    AppSecret = account.AppSecret;
                    ExcludeOwnedStocks = account.ExcludeOwnedStocks;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"계좌 정보 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// AppKey 표시/숨김 토글
        /// </summary>
        private void ToggleAppKeyVisibility()
        {
            // TODO: PasswordBox의 표시/숨김 토글 구현
            MessageBox.Show("AppKey 표시/숨김 기능은 추후 구현 예정입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// AppSecret 표시/숨김 토글
        /// </summary>
        private void ToggleAppSecretVisibility()
        {
            // TODO: PasswordBox의 표시/숨김 토글 구현
            MessageBox.Show("AppSecret 표시/숨김 기능은 추후 구현 예정입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// API 연결 테스트
        /// </summary>
        private async void TestApiConnection()
        {
            // 입력 값 검증
            if (string.IsNullOrWhiteSpace(AppKey) || string.IsNullOrWhiteSpace(AppSecret))
            {
                ApiTestStatus = "❌ AppKey와 AppSecret을 입력해주세요";
                ApiTestStatusColor = new SolidColorBrush(Colors.Red);
                return;
            }

            ApiTestStatus = "🔄 연결 테스트 중...";
            ApiTestStatusColor = new SolidColorBrush(Colors.Orange);

            try
            {
                // KisSettings 임시 생성 (실전 투자)
                var kisSettings = new KisSettings
                {
                    AppKey = AppKey,
                    AppSecret = AppSecret,
                    IsPaperTrading = false, // 실전 투자
                    BaseUrl = "https://openapi.koreainvestment.com:9443" // 실전 투자 URL
                };

                // HttpClient 생성
                var httpClient = new System.Net.Http.HttpClient();

                // NullLogger 생성 (테스트용)
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger<KisAuthService>();

                // KisAuthService 생성 및 토큰 획득 시도
                var authService = new KisAuthService(
                    httpClient,
                    Options.Create(kisSettings),
                    logger
                );

                // 액세스 토큰 획득 시도
                var accessToken = await authService.GetAccessTokenAsync();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    ApiTestStatus = "✅ API 연결 성공 (실전투자)";
                    ApiTestStatusColor = new SolidColorBrush(Colors.Green);
                    MessageBox.Show(
                        "한국투자증권 API 연결에 성공했습니다!\n\n" +
                        "액세스 토큰이 정상적으로 발급되었습니다.\n\n" +
                        "⚠️ 주의: 실전 투자 계좌로 연결됩니다.",
                        "API 연결 성공",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    ApiTestStatus = "❌ API 연결 실패 (토큰 발급 실패)";
                    ApiTestStatusColor = new SolidColorBrush(Colors.Red);
                    MessageBox.Show(
                        "API 연결에 실패했습니다.\n\n" +
                        "AppKey와 AppSecret을 확인해주세요.",
                        "API 연결 실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                ApiTestStatus = $"❌ API 연결 실패: {ex.Message}";
                ApiTestStatusColor = new SolidColorBrush(Colors.Red);
                MessageBox.Show(
                    $"API 연결 테스트 중 오류가 발생했습니다:\n\n{ex.Message}\n\n" +
                    "AppKey와 AppSecret이 올바른지 확인해주세요.",
                    "API 연결 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 저장
        /// </summary>
        private async void Save()
        {
            // 유효성 검사
            if (string.IsNullOrWhiteSpace(AccountNumber))
            {
                MessageBox.Show("계좌번호를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(AppKey))
            {
                MessageBox.Show("AppKey를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(AppSecret))
            {
                MessageBox.Show("AppSecret을 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_accountId.HasValue)
                {
                    // 편집 모드: 기존 계좌 업데이트
                    var account = await _accountRepository.GetAccountByIdAsync(_accountId.Value);
                    if (account != null)
                    {
                        account.AccountNumber = AccountNumber;
                        account.AccountName = AccountName;
                        account.AppKey = AppKey;
                        account.AppSecret = AppSecret;
                        account.ExcludeOwnedStocks = ExcludeOwnedStocks;

                        await _accountRepository.UpdateAccountAsync(account);
                        MessageBox.Show("계좌 정보가 수정되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // 신규 모드: 새 계좌 추가
                    var newAccount = new Account
                    {
                        AccountNumber = AccountNumber,
                        AccountName = AccountName,
                        AppKey = AppKey,
                        AppSecret = AppSecret,
                        IsActive = false,
                        ExcludeOwnedStocks = ExcludeOwnedStocks
                    };

                    await _accountRepository.AddAccountAsync(newAccount);
                    MessageBox.Show("계좌가 추가되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // 모달 닫기 (DialogResult = true로 설정하여 부모에게 성공 알림)
                var dialog = Application.Current.Windows.OfType<AccountDetailDialog>().FirstOrDefault();
                if (dialog != null)
                {
                    dialog.DialogResult = true;
                    dialog.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 취소
        /// </summary>
        private void Cancel()
        {
            // 모달 닫기
            Application.Current.Windows.OfType<AccountDetailDialog>().FirstOrDefault()?.Close();
        }

        #endregion
    }
}
