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
        // DataContext는 부모 컨테이너나 DI에서 설정됨
    }

    /// <summary>
    /// 조건 추가 버튼 클릭 이벤트 핸들러
    /// </summary>
    private void AddConditionButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new ConditionEditorDialog();
        if (dialog.ShowDialog() == true && dialog.ResultCondition != null)
        {
            if (DataContext is ConditionBuilderViewModel viewModel)
            {
                viewModel.Conditions.Add(dialog.ResultCondition);
            }
        }
    }
}
