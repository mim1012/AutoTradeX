using System.Windows.Controls;
using AutoTrader.UI.ViewModels;

namespace AutoTrader.UI.Views;

/// <summary>
/// ConditionBuilderControl.xaml에 대한 상호 작용 논리
/// </summary>
public partial class ConditionBuilderControl : UserControl
{
    public ConditionBuilderControl()
    {
        InitializeComponent();
        DataContext = new ConditionBuilderViewModel();
    }
}
