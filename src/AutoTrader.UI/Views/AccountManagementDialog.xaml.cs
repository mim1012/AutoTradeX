using System.Windows;
using AutoTrader.UI.ViewModels;

namespace AutoTrader.UI.Views
{
    /// <summary>
    /// AccountManagementDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AccountManagementDialog : Window
    {
        public AccountManagementDialog(AccountManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
