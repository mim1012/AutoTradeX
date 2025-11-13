using System.Windows;
using System.Windows.Controls;
using AutoTrader.UI.ViewModels;
using AutoTrader.UI.Models;

namespace AutoTrader.UI.Views;

/// <summary>
/// ConditionSetEditorDialog.xaml에 대한 상호 작용 논리
/// </summary>
public partial class ConditionSetEditorDialog : Window
{
    private readonly ConditionSetEditorViewModel _viewModel;

    public ConditionSetEditorDialog()
    {
        InitializeComponent();
        _viewModel = new ConditionSetEditorViewModel();
        DataContext = _viewModel;

        // 초기화
        LoadConditions();
        UpdateFormulaEditor();
    }

    public ConditionSetEditorDialog(int accountId) : this()
    {
        // 특정 계좌의 조건식 로드
        _viewModel.LoadConditionSet(accountId);
        LoadConditions();
        UpdateFormulaEditor();
    }

    /// <summary>
    /// 조건 목록 로드
    /// </summary>
    private void LoadConditions()
    {
        ConditionsDataGrid.ItemsSource = _viewModel.Conditions;
        MatchedStocksDataGrid.ItemsSource = _viewModel.MatchedStocks;
    }

    /// <summary>
    /// 조건식 편집기 업데이트
    /// </summary>
    private void UpdateFormulaEditor()
    {
        FormulaEditorPanel.Children.Clear();

        foreach (var token in _viewModel.FormulaTokens)
        {
            if (token.Type == FormulaTokenType.ConditionId)
            {
                // 조건 ID 버튼
                var button = new Button
                {
                    Content = token.Value,
                    Width = 40,
                    Height = 30,
                    Margin = new Thickness(2),
                    Tag = token
                };
                button.Click += ConditionIdButton_Click;
                FormulaEditorPanel.Children.Add(button);
            }
            else if (token.Type == FormulaTokenType.Operator)
            {
                // AND/OR 드롭다운
                var comboBox = new ComboBox
                {
                    Width = 70,
                    Height = 30,
                    Margin = new Thickness(2),
                    Tag = token
                };
                comboBox.Items.Add(new ComboBoxItem { Content = "and", Tag = "and" });
                comboBox.Items.Add(new ComboBoxItem { Content = "or", Tag = "or" });
                comboBox.SelectedIndex = token.Value == "and" ? 0 : 1;
                comboBox.SelectionChanged += OperatorComboBox_SelectionChanged;
                FormulaEditorPanel.Children.Add(comboBox);
            }
            else if (token.Type == FormulaTokenType.OpenParen || token.Type == FormulaTokenType.CloseParen)
            {
                // 괄호
                var textBlock = new TextBlock
                {
                    Text = token.Value,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                FormulaEditorPanel.Children.Add(textBlock);
            }
        }

        // 조건식 미리보기 업데이트
        FormulaPreviewTextBlock.Text = _viewModel.GetFormulaPreview();
    }

    /// <summary>
    /// 조건 추가 버튼 클릭
    /// </summary>
    private void AddConditionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Conditions.Count >= 10)
        {
            MessageBox.Show("조건은 최대 10개까지만 추가할 수 있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new ConditionEditorDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.ResultCondition != null)
        {
            _viewModel.AddCondition(dialog.ResultCondition);
            LoadConditions();
            UpdateFormulaEditor();
        }
    }

    /// <summary>
    /// 조건 삭제 버튼 클릭
    /// </summary>
    private void DeleteConditionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ConditionItemViewModel condition)
        {
            var result = MessageBox.Show(
                $"조건 '{condition.ConditionId}'를 삭제하시겠습니까?",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.RemoveCondition(condition);
                LoadConditions();
                UpdateFormulaEditor();
            }
        }
    }

    /// <summary>
    /// 조건 위로 이동 버튼 클릭
    /// </summary>
    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ConditionItemViewModel condition)
        {
            _viewModel.MoveConditionUp(condition);
            LoadConditions();
            UpdateFormulaEditor();
        }
    }

    /// <summary>
    /// 조건 아래로 이동 버튼 클릭
    /// </summary>
    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ConditionItemViewModel condition)
        {
            _viewModel.MoveConditionDown(condition);
            LoadConditions();
            UpdateFormulaEditor();
        }
    }

    /// <summary>
    /// 조건 ID 버튼 클릭
    /// </summary>
    private void ConditionIdButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is FormulaToken token)
        {
            // 조건 ID 클릭 시 조건식에서 제거
            _viewModel.RemoveFormulaToken(token);
            UpdateFormulaEditor();
        }
    }

    /// <summary>
    /// AND/OR 드롭다운 선택 변경
    /// </summary>
    private void OperatorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.Tag is FormulaToken token && comboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var newOperator = selectedItem.Tag?.ToString() ?? "and";
            _viewModel.UpdateOperator(token, newOperator);
            UpdateFormulaEditor();
        }
    }

    /// <summary>
    /// 여는 괄호 추가 버튼 클릭
    /// </summary>
    private void AddOpenParenButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddOpenParen();
        UpdateFormulaEditor();
    }

    /// <summary>
    /// 닫는 괄호 추가 버튼 클릭
    /// </summary>
    private void AddCloseParenButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddCloseParen();
        UpdateFormulaEditor();
    }

    /// <summary>
    /// 마지막 토큰 제거 버튼 클릭
    /// </summary>
    private void RemoveLastTokenButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RemoveLastToken();
        UpdateFormulaEditor();
    }

    /// <summary>
    /// 저장 버튼 클릭
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // 조건식 유효성 검사
        if (!_viewModel.ValidateFormula())
        {
            MessageBox.Show("조건식이 유효하지 않습니다. 괄호가 올바르게 닫혀있는지 확인하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // DB에 저장
        try
        {
            _viewModel.SaveConditionSet();
            MessageBox.Show("조건식이 저장되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 취소 버튼 클릭
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
