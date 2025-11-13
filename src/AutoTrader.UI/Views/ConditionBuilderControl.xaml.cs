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
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ConditionBuilderViewModel viewModel)
        {
            // LogicOperator에 따라 RadioButton 초기 상태 설정
            if (viewModel.LogicOperator.ToUpper() == "AND")
            {
                AndRadioButton.IsChecked = true;
            }
            else
            {
                OrRadioButton.IsChecked = true;
            }
        }
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

    /// <summary>
    /// AND RadioButton 체크 이벤트
    /// </summary>
    private void AndRadioButton_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ConditionBuilderViewModel viewModel)
        {
            viewModel.LogicOperator = "AND";
        }
    }

    /// <summary>
    /// OR RadioButton 체크 이벤트
    /// </summary>
    private void OrRadioButton_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ConditionBuilderViewModel viewModel)
        {
            viewModel.LogicOperator = "OR";
        }
    }
}
