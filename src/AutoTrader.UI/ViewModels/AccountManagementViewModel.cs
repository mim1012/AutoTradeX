using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Repositories;
using AutoTrader.UI.Commands;
using AutoTrader.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AutoTrader.UI.ViewModels
{
    /// <summary>
    /// 계좌 관리 모달 ViewModel
    /// </summary>
    public class AccountManagementViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;
        private readonly AccountRepository _accountRepository;
        private readonly ConditionSetRepository _conditionSetRepository;

        private Account? _selectedAccount;

        public AccountManagementViewModel()
        {
            // DbContext 초기화
            var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite("Data Source=autotrader.db");
            _dbContext = new AppDbContext(optionsBuilder.Options);

            // Repository 초기화
            _accountRepository = new AccountRepository(_dbContext);
            _conditionSetRepository = new ConditionSetRepository(_dbContext);

            // Command 초기화
            SetActiveAccountCommand = new RelayCommand<Account>(SetActiveAccount);
            AddAccountCommand = new RelayCommand(AddAccount);
            EditAccountCommand = new RelayCommand<Account>(EditAccount);
            DeleteAccountCommand = new RelayCommand<Account>(DeleteAccount);
            ConfirmCommand = new RelayCommand(Confirm);
            CancelCommand = new RelayCommand(Cancel);

            // 초기 데이터 로드
            _ = LoadAccountsAsync();
        }

        #region Properties

        /// <summary>
        /// 등록된 계좌 목록
        /// </summary>
        public ObservableCollection<AccountViewModel> Accounts { get; } = new ObservableCollection<AccountViewModel>();

        /// <summary>
        /// 선택된 계좌
        /// </summary>
        public Account? SelectedAccount
        {
            get => _selectedAccount;
            set => SetProperty(ref _selectedAccount, value);
        }

        #endregion

        #region Commands

        public ICommand SetActiveAccountCommand { get; }
        public ICommand AddAccountCommand { get; }
        public ICommand EditAccountCommand { get; }
        public ICommand DeleteAccountCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Methods

        /// <summary>
        /// 계좌 목록 로드
        /// </summary>
        private async Task LoadAccountsAsync()
        {
            try
            {
                var accounts = await _accountRepository.GetAllAccountsAsync();
                Accounts.Clear();

                foreach (var account in accounts)
                {
                    var hasConditionSet = await _conditionSetRepository.HasConditionSetAsync(account.AccountId);
                    Accounts.Add(new AccountViewModel
                    {
                        AccountId = account.AccountId,
                        AccountNumber = account.AccountNumber,
                        AccountName = account.AccountName ?? string.Empty,
                        IsActive = account.IsActive,
                        ConditionSetStatus = hasConditionSet ? "✅ 설정" : "❌ 미설정"
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"계좌 목록 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 활성 계좌 설정
        /// </summary>
        private async void SetActiveAccount(Account? account)
        {
            if (account == null) return;

            try
            {
                await _accountRepository.SetActiveAccountAsync(account.AccountId);

                // UI 업데이트
                foreach (var acc in Accounts)
                {
                    acc.IsActive = acc.AccountId == account.AccountId;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"활성 계좌 설정 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 계좌 추가
        /// </summary>
        private void AddAccount()
        {
            var dialog = new AccountDetailDialog
            {
                Owner = Application.Current.Windows.OfType<AccountManagementDialog>().FirstOrDefault()
            };

            if (dialog.ShowDialog() == true)
            {
                // 계좌 추가 후 다시 로드
                _ = LoadAccountsAsync();
            }
        }

        /// <summary>
        /// 계좌 편집
        /// </summary>
        private void EditAccount(Account? account)
        {
            if (account == null) return;

            var dialog = new AccountDetailDialog
            {
                Owner = Application.Current.Windows.OfType<AccountManagementDialog>().FirstOrDefault(),
                DataContext = new AccountDetailViewModel(account.AccountId)
            };

            if (dialog.ShowDialog() == true)
            {
                // 계좌 편집 후 다시 로드
                _ = LoadAccountsAsync();
            }
        }

        /// <summary>
        /// 계좌 삭제
        /// </summary>
        private async void DeleteAccount(Account? account)
        {
            if (account == null) return;

            var result = MessageBox.Show(
                $"계좌 '{account.AccountNumber}'를 삭제하시겠습니까?\n연결된 조건식도 함께 삭제됩니다.",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _accountRepository.DeleteAccountAsync(account.AccountId);
                    await LoadAccountsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"계좌 삭제 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 확인
        /// </summary>
        private void Confirm()
        {
            // 모달 닫기
            Application.Current.Windows.OfType<AccountManagementDialog>().FirstOrDefault()?.Close();
        }

        /// <summary>
        /// 취소
        /// </summary>
        private void Cancel()
        {
            // 모달 닫기
            Application.Current.Windows.OfType<AccountManagementDialog>().FirstOrDefault()?.Close();
        }

        #endregion
    }

    /// <summary>
    /// 계좌 정보 ViewModel (DataGrid 바인딩용)
    /// </summary>
    public class AccountViewModel : ViewModelBase
    {
        private bool _isActive;

        public int AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public string ConditionSetStatus { get; set; } = string.Empty;
    }
}
