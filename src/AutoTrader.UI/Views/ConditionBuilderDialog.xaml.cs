using System.Windows;
using AutoTrader.UI.ViewModels;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Data;
using AutoTrader.Core.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoTrader.UI.Views;

/// <summary>
/// ConditionBuilderDialog.xaml에 대한 상호 작용 논리
/// </summary>
public partial class ConditionBuilderDialog : Window
{
    public ConditionBuilderDialog()
    {
        InitializeComponent();

        // DbContext 초기화
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=autotrader.db");
        var dbContext = new AppDbContext(optionsBuilder.Options);

        // EncryptionService 초기화
        var encryptionService = new EncryptionService(NullLogger<EncryptionService>.Instance);

        // Repository 초기화
        var accountRepository = new AccountRepository(dbContext, encryptionService);
        var conditionSetRepository = new ConditionSetRepository(dbContext);

        // ViewModel 생성 및 설정
        DataContext = new ConditionBuilderViewModel(accountRepository, conditionSetRepository);
    }
}
