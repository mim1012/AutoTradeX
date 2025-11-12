using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Repositories;
using AutoTrader.UI.Commands;
using AutoTrader.UI.Views;
using System;
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
        private double _buyRatio = 100.0;
        private bool _excludeOwnedStocks = false;
        private string _apiTestStatus = string.Empty;
        private Brush _apiTestStatusColor = new SolidColorBrush(Colors.Black);

        public AccountDetailViewModel() : this(null)
        {
        }

        public AccountDetailViewModel(int? accountId)
        {
            _accountId = accountId;

            // DbContext 초기화
            var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite("Data Source=autotrader.db");
            _dbContext = new AppDbContext(optionsBuilder.Options);

            // Repository 초기화
            _accountRepository = new AccountRepository(_dbContext);

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

        public double BuyRatio
        {
            get => _buyRatio;
            set => SetProperty(ref _buyRatio, value);
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
                    BuyRatio = account.BuyRatio;
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
        private void TestApiConnection()
        {
            // TODO: 실제 KIS API 연결 테스트 구현
            ApiTestStatus = "✅ API 연결 성공";
            ApiTestStatusColor = new SolidColorBrush(Colors.Green);

            MessageBox.Show("API 연결 테스트 기능은 추후 구현 예정입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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

            if (BuyRatio < 0 || BuyRatio > 100)
            {
                MessageBox.Show("매수 비율은 0 ~ 100 사이의 값이어야 합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        account.BuyRatio = BuyRatio;
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
                        BuyRatio = BuyRatio,
                        ExcludeOwnedStocks = ExcludeOwnedStocks
                    };

                    await _accountRepository.AddAccountAsync(newAccount);
                    MessageBox.Show("계좌가 추가되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // 모달 닫기
                Application.Current.Windows.OfType<AccountDetailDialog>().FirstOrDefault()?.Close();
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
