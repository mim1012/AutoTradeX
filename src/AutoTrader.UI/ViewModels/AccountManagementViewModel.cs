using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Services.Security;
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
            _conditionSetRepository = new ConditionSetRepository(_dbContext);

            // Command 초기화
            SetActiveAccountCommand = new RelayCommand(param => SetActiveAccount(param as AccountViewModel));
            AddAccountCommand = new RelayCommand(_ => AddAccount());
            EditAccountCommand = new RelayCommand(param => EditAccount(param as AccountViewModel));
            DeleteAccountCommand = new RelayCommand(param => DeleteAccount(param as AccountViewModel));
            CopyConditionSetCommand = new RelayCommand(param => CopyConditionSet(param as AccountViewModel));
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
        public ICommand CopyConditionSetCommand { get; }
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
        private async void SetActiveAccount(AccountViewModel? accountViewModel)
        {
            if (accountViewModel == null) return;

            try
            {
                await _accountRepository.SetActiveAccountAsync(accountViewModel.AccountId);

                // UI 업데이트
                foreach (var acc in Accounts)
                {
                    acc.IsActive = acc.AccountId == accountViewModel.AccountId;
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
                Owner = Application.Current.Windows.OfType<AccountManagementDialog>().FirstOrDefault(),
                DataContext = new AccountDetailViewModel(null) // 신규 추가
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
        private void EditAccount(AccountViewModel? accountViewModel)
        {
            if (accountViewModel == null) return;

            var dialog = new AccountDetailDialog
            {
                Owner = Application.Current.Windows.OfType<AccountManagementDialog>().FirstOrDefault(),
                DataContext = new AccountDetailViewModel(accountViewModel.AccountId)
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
        private async void DeleteAccount(AccountViewModel? accountViewModel)
        {
            if (accountViewModel == null) return;

            var result = MessageBox.Show(
                $"계좌 '{accountViewModel.AccountNumber}'를 삭제하시겠습니까?\n연결된 조건식도 함께 삭제됩니다.",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _accountRepository.DeleteAccountAsync(accountViewModel.AccountId);
                    await LoadAccountsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"계좌 삭제 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 조건식 복사
        /// </summary>
        private async void CopyConditionSet(AccountViewModel? targetAccountViewModel)
        {
            if (targetAccountViewModel == null) return;

            try
            {
                // 모든 조건식 목록 조회
                var allConditionSets = await _conditionSetRepository.GetAllConditionSetsAsync();

                if (allConditionSets.Count == 0)
                {
                    MessageBox.Show("복사할 조건식이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 복사할 원본 계좌 선택 다이얼로그 (간단한 구현)
                var sourceOptions = string.Join("\n", allConditionSets.Select((cs, idx) =>
                    $"{idx + 1}. {cs.Account?.AccountNumber} - {cs.Name} ({cs.Conditions.Count}개 조건)"));

                var input = Microsoft.VisualBasic.Interaction.InputBox(
                    $"복사할 조건식 번호를 입력하세요:\n\n{sourceOptions}",
                    "조건식 복사",
                    "1");

                if (string.IsNullOrWhiteSpace(input))
                    return;

                if (!int.TryParse(input, out var selectedIndex) || selectedIndex < 1 || selectedIndex > allConditionSets.Count)
                {
                    MessageBox.Show("잘못된 번호입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var sourceAccountId = allConditionSets[selectedIndex - 1].AccountId;

                // 확인 메시지
                var confirmResult = MessageBox.Show(
                    $"'{allConditionSets[selectedIndex - 1].Name}' 조건식을\n" +
                    $"'{targetAccountViewModel.AccountNumber}' 계좌로 복사하시겠습니까?\n\n" +
                    $"⚠️ 기존 조건식이 있다면 덮어씌워집니다.",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.Yes)
                    return;

                // 조건식 복사 실행
                await _conditionSetRepository.CopyConditionSetAsync(sourceAccountId, targetAccountViewModel.AccountId);

                MessageBox.Show("조건식이 성공적으로 복사되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);

                // 목록 새로고침
                await LoadAccountsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"조건식 복사 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 확인
        /// </summary>
        private void Confirm()
        {
            // 모달 닫기 (DialogResult = true로 설정하여 부모에게 변경사항 알림)
            var dialog = Application.Current.Windows.OfType<AccountManagementDialog>().FirstOrDefault();
            if (dialog != null)
            {
                dialog.DialogResult = true;
                dialog.Close();
            }
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
