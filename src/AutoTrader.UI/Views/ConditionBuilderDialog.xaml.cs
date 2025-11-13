using System.Windows;
using AutoTrader.UI.ViewModels;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Data;
using Microsoft.EntityFrameworkCore;

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

        // Repository 초기화
        var accountRepository = new AccountRepository(dbContext);
        var conditionSetRepository = new ConditionSetRepository(dbContext);

        // ViewModel 생성 및 설정
        DataContext = new ConditionBuilderViewModel(accountRepository, conditionSetRepository);
    }
}
