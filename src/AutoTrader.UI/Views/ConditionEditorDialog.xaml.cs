using System.Windows;
using System.Windows.Controls;
using AutoTrader.UI.Models;
using AutoTrader.UI.ViewModels;

namespace AutoTrader.UI.Views;

/// <summary>
/// ConditionEditorDialog.xaml에 대한 상호 작용 논리
/// </summary>
public partial class ConditionEditorDialog : Window
{
    public ConditionItemViewModel? ResultCondition { get; private set; }
    
    // 동적 입력 컨트롤 참조
    private ComboBox? _candleTypeComboBox;
    private TextBox? _minPercentTextBox;
    private TextBox? _maxPercentTextBox;
    private ComboBox? _maTypeComboBox;
    private TextBox? _minTradeVolumeTextBox;
    private ComboBox? _priceElementAComboBox;
    private ComboBox? _priceElementBComboBox;
    private ComboBox? _comparisonOperatorComboBox;

    public ConditionEditorDialog()
    {
        InitializeComponent();
    }

    public ConditionEditorDialog(ConditionItemViewModel existingCondition) : this()
    {
        // 편집 모드: 기존 조건 데이터 로드
        Title = "조건 편집";
        LoadExistingCondition(existingCondition);
    }

    private void ConditionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConditionTypeComboBox.SelectedItem is not ComboBoxItem selectedItem)
            return;

        var conditionTypeTag = selectedItem.Tag?.ToString();
        if (string.IsNullOrEmpty(conditionTypeTag))
            return;

        // 선택된 조건 타입에 따라 동적 폼 생성
        switch (conditionTypeTag)
        {
            case "PriceChange":
                BuildPriceChangeForm();
                break;
            case "MovingAverage":
                BuildMovingAverageForm();
                break;
            case "TradeVolume":
                BuildTradeVolumeForm();
                break;
            case "PriceComparison":
                BuildPriceComparisonForm();
                break;
        }
    }

    private void BuildPriceChangeForm()
    {
        DynamicFormPanel.Children.Clear();

        // 기준봉 선택
        var candleLabel = new TextBlock { Text = "기준봉", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        _candleTypeComboBox = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 16),
            Height = 36,
            Padding = new Thickness(8)
        };
        _candleTypeComboBox.Items.Add(new ComboBoxItem { Content = "일봉", Tag = "Daily" });
        _candleTypeComboBox.Items.Add(new ComboBoxItem { Content = "주봉", Tag = "Weekly" });
        _candleTypeComboBox.SelectedIndex = 0;

        // 등락률 범위
        var rangeLabel = new TextBlock { Text = "등락률 범위 (%)", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        
        var rangePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        _minPercentTextBox = new TextBox
        {
            Width = 100,
            Height = 36,
            Padding = new Thickness(8),
            Text = "-7.0",
            Margin = new Thickness(0, 0, 8, 0)
        };
        var tildeText = new TextBlock { Text = "~", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
        _maxPercentTextBox = new TextBox
        {
            Width = 100,
            Height = 36,
            Padding = new Thickness(8),
            Text = "0.0",
            Margin = new Thickness(8, 0, 0, 0)
        };
        
        rangePanel.Children.Add(_minPercentTextBox);
        rangePanel.Children.Add(tildeText);
        rangePanel.Children.Add(_maxPercentTextBox);

        DynamicFormPanel.Children.Add(candleLabel);
        DynamicFormPanel.Children.Add(_candleTypeComboBox);
        DynamicFormPanel.Children.Add(rangeLabel);
        DynamicFormPanel.Children.Add(rangePanel);

        // 입력 변경 시 미리보기 업데이트
        _candleTypeComboBox.SelectionChanged += (s, e) => UpdatePreview();
        _minPercentTextBox.TextChanged += (s, e) => UpdatePreview();
        _maxPercentTextBox.TextChanged += (s, e) => UpdatePreview();

        UpdatePreview();
    }

    private void BuildMovingAverageForm()
    {
        DynamicFormPanel.Children.Clear();

        // 이동평균선 선택
        var maLabel = new TextBlock { Text = "이동평균선", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        _maTypeComboBox = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 16),
            Height = 36,
            Padding = new Thickness(8)
        };
        _maTypeComboBox.Items.Add(new ComboBoxItem { Content = "10일 이평선", Tag = "10" });
        _maTypeComboBox.Items.Add(new ComboBoxItem { Content = "20일 이평선", Tag = "20" });
        _maTypeComboBox.Items.Add(new ComboBoxItem { Content = "120일 이평선", Tag = "120" });
        _maTypeComboBox.SelectedIndex = 1; // 기본값: 20일

        // 범위
        var rangeLabel = new TextBlock { Text = "현재가 대비 범위 (%)", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        
        var rangePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        _minPercentTextBox = new TextBox
        {
            Width = 100,
            Height = 36,
            Padding = new Thickness(8),
            Text = "-3.0",
            Margin = new Thickness(0, 0, 8, 0)
        };
        var tildeText = new TextBlock { Text = "~", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
        _maxPercentTextBox = new TextBox
        {
            Width = 100,
            Height = 36,
            Padding = new Thickness(8),
            Text = "+3.0",
            Margin = new Thickness(8, 0, 0, 0)
        };
        
        rangePanel.Children.Add(_minPercentTextBox);
        rangePanel.Children.Add(tildeText);
        rangePanel.Children.Add(_maxPercentTextBox);

        DynamicFormPanel.Children.Add(maLabel);
        DynamicFormPanel.Children.Add(_maTypeComboBox);
        DynamicFormPanel.Children.Add(rangeLabel);
        DynamicFormPanel.Children.Add(rangePanel);

        _maTypeComboBox.SelectionChanged += (s, e) => UpdatePreview();
        _minPercentTextBox.TextChanged += (s, e) => UpdatePreview();
        _maxPercentTextBox.TextChanged += (s, e) => UpdatePreview();

        UpdatePreview();
    }

    private void BuildTradeVolumeForm()
    {
        DynamicFormPanel.Children.Clear();

        // 최소 거래대금
        var volumeLabel = new TextBlock { Text = "최소 거래대금 (만 달러)", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        _minTradeVolumeTextBox = new TextBox
        {
            Height = 36,
            Padding = new Thickness(8),
            Text = "1000",
            Margin = new Thickness(0, 0, 0, 16)
        };

        DynamicFormPanel.Children.Add(volumeLabel);
        DynamicFormPanel.Children.Add(_minTradeVolumeTextBox);

        _minTradeVolumeTextBox.TextChanged += (s, e) => UpdatePreview();

        UpdatePreview();
    }

    private void BuildPriceComparisonForm()
    {
        DynamicFormPanel.Children.Clear();

        // 비교식 A
        var elementALabel = new TextBlock { Text = "비교 요소 A", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        _priceElementAComboBox = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 16),
            Height = 36,
            Padding = new Thickness(8)
        };
        _priceElementAComboBox.Items.Add(new ComboBoxItem { Content = "0봉 시가", Tag = "0_Open" });
        _priceElementAComboBox.Items.Add(new ComboBoxItem { Content = "0봉 종가", Tag = "0_Close" });
        _priceElementAComboBox.Items.Add(new ComboBoxItem { Content = "0봉 고가", Tag = "0_High" });
        _priceElementAComboBox.Items.Add(new ComboBoxItem { Content = "0봉 저가", Tag = "0_Low" });
        _priceElementAComboBox.Items.Add(new ComboBoxItem { Content = "1봉 시가", Tag = "1_Open" });
        _priceElementAComboBox.Items.Add(new ComboBoxItem { Content = "1봉 종가", Tag = "1_Close" });
        _priceElementAComboBox.Items.Add(new ComboBoxItem { Content = "1봉 고가", Tag = "1_High" });
        _priceElementAComboBox.Items.Add(new ComboBoxItem { Content = "1봉 저가", Tag = "1_Low" });
        _priceElementAComboBox.SelectedIndex = 0;

        // 비교 연산자
        var operatorLabel = new TextBlock { Text = "비교 연산자", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        _comparisonOperatorComboBox = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 16),
            Height = 36,
            Padding = new Thickness(8)
        };
        _comparisonOperatorComboBox.Items.Add(new ComboBoxItem { Content = ">  (크다)", Tag = ">" });
        _comparisonOperatorComboBox.Items.Add(new ComboBoxItem { Content = "<  (작다)", Tag = "<" });
        _comparisonOperatorComboBox.Items.Add(new ComboBoxItem { Content = "≥  (크거나 같다)", Tag = ">=" });
        _comparisonOperatorComboBox.Items.Add(new ComboBoxItem { Content = "≤  (작거나 같다)", Tag = "<=" });
        _comparisonOperatorComboBox.SelectedIndex = 0;

        // 비교식 B
        var elementBLabel = new TextBlock { Text = "비교 요소 B", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        _priceElementBComboBox = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 16),
            Height = 36,
            Padding = new Thickness(8)
        };
        _priceElementBComboBox.Items.Add(new ComboBoxItem { Content = "0봉 시가", Tag = "0_Open" });
        _priceElementBComboBox.Items.Add(new ComboBoxItem { Content = "0봉 종가", Tag = "0_Close" });
        _priceElementBComboBox.Items.Add(new ComboBoxItem { Content = "0봉 고가", Tag = "0_High" });
        _priceElementBComboBox.Items.Add(new ComboBoxItem { Content = "0봉 저가", Tag = "0_Low" });
        _priceElementBComboBox.Items.Add(new ComboBoxItem { Content = "1봉 시가", Tag = "1_Open" });
        _priceElementBComboBox.Items.Add(new ComboBoxItem { Content = "1봉 종가", Tag = "1_Close" });
        _priceElementBComboBox.Items.Add(new ComboBoxItem { Content = "1봉 고가", Tag = "1_High" });
        _priceElementBComboBox.Items.Add(new ComboBoxItem { Content = "1봉 저가", Tag = "1_Low" });
        _priceElementBComboBox.SelectedIndex = 7; // 기본값: 1봉 저가

        DynamicFormPanel.Children.Add(elementALabel);
        DynamicFormPanel.Children.Add(_priceElementAComboBox);
        DynamicFormPanel.Children.Add(operatorLabel);
        DynamicFormPanel.Children.Add(_comparisonOperatorComboBox);
        DynamicFormPanel.Children.Add(elementBLabel);
        DynamicFormPanel.Children.Add(_priceElementBComboBox);

        _priceElementAComboBox.SelectionChanged += (s, e) => UpdatePreview();
        _comparisonOperatorComboBox.SelectionChanged += (s, e) => UpdatePreview();
        _priceElementBComboBox.SelectionChanged += (s, e) => UpdatePreview();

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (ConditionTypeComboBox.SelectedItem is not ComboBoxItem selectedItem)
            return;

        var conditionTypeTag = selectedItem.Tag?.ToString();
        if (string.IsNullOrEmpty(conditionTypeTag))
            return;

        string preview = conditionTypeTag switch
        {
            "PriceChange" => GeneratePriceChangePreview(),
            "MovingAverage" => GenerateMovingAveragePreview(),
            "TradeVolume" => GenerateTradeVolumePreview(),
            "PriceComparison" => GeneratePriceComparisonPreview(),
            _ => "조건을 설정하세요"
        };

        PreviewTextBlock.Text = preview;
    }

    private string GeneratePriceChangePreview()
    {
        if (_candleTypeComboBox == null || _minPercentTextBox == null || _maxPercentTextBox == null)
            return "등락률 조건을 설정하세요";

        var candleType = (_candleTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "일봉";
        var minPercent = _minPercentTextBox.Text;
        var maxPercent = _maxPercentTextBox.Text;

        return $"등락률: [{candleType}] 0봉 전 종가 대비 {minPercent}% ~ {maxPercent}%";
    }

    private string GenerateMovingAveragePreview()
    {
        if (_maTypeComboBox == null || _minPercentTextBox == null || _maxPercentTextBox == null)
            return "이동평균선 조건을 설정하세요";

        var maType = (_maTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "20일 이평선";
        var minPercent = _minPercentTextBox.Text;
        var maxPercent = _maxPercentTextBox.Text;

        return $"이동평균: 현재가가 {maType} 대비 {minPercent}% ~ {maxPercent}% 이내";
    }

    private string GenerateTradeVolumePreview()
    {
        if (_minTradeVolumeTextBox == null)
            return "거래대금 조건을 설정하세요";

        var minVolume = _minTradeVolumeTextBox.Text;
        return $"거래대금: {minVolume}만 달러 이상";
    }

    private string GeneratePriceComparisonPreview()
    {
        if (_priceElementAComboBox == null || _comparisonOperatorComboBox == null || _priceElementBComboBox == null)
            return "주가 비교 조건을 설정하세요";

        var elementA = (_priceElementAComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        var op = (_comparisonOperatorComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        var elementB = (_priceElementBComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        return $"주가비교: ({elementA}) {op} ({elementB})";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ConditionTypeComboBox.SelectedItem is not ComboBoxItem selectedItem)
        {
            MessageBox.Show("조건 타입을 선택하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var conditionTypeTag = selectedItem.Tag?.ToString();
        if (!Enum.TryParse<ConditionType>(conditionTypeTag, out var conditionType))
        {
            MessageBox.Show("유효하지 않은 조건 타입입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // 조건 생성
        ResultCondition = new ConditionItemViewModel
        {
            Type = conditionType,
            Description = PreviewTextBlock.Text,
            IsEnabled = true,
            StatusMessage = "- (미평가)"
        };

        // 조건 타입별 파라미터 저장
        try
        {
            switch (conditionTypeTag)
            {
                case "PriceChange":
                    SavePriceChangeParameters(ResultCondition);
                    break;
                case "MovingAverage":
                    SaveMovingAverageParameters(ResultCondition);
                    break;
                case "TradeVolume":
                    SaveTradeVolumeParameters(ResultCondition);
                    break;
                case "PriceComparison":
                    SavePriceComparisonParameters(ResultCondition);
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파라미터 저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void SavePriceChangeParameters(ConditionItemViewModel condition)
    {
        if (_candleTypeComboBox?.SelectedItem is ComboBoxItem candleItem)
        {
            condition.Parameters["CandleType"] = candleItem.Tag?.ToString() ?? "Daily";
        }

        if (_minPercentTextBox != null && decimal.TryParse(_minPercentTextBox.Text, out var minPercent))
        {
            condition.Parameters["MinPercent"] = minPercent;
        }

        if (_maxPercentTextBox != null && decimal.TryParse(_maxPercentTextBox.Text, out var maxPercent))
        {
            condition.Parameters["MaxPercent"] = maxPercent;
        }
    }

    private void SaveMovingAverageParameters(ConditionItemViewModel condition)
    {
        if (_maTypeComboBox?.SelectedItem is ComboBoxItem maItem && int.TryParse(maItem.Tag?.ToString(), out var maType))
        {
            condition.Parameters["MaType"] = maType; // 10, 20, or 120
        }

        if (_minPercentTextBox != null && decimal.TryParse(_minPercentTextBox.Text, out var minPercent))
        {
            condition.Parameters["MaRangeMin"] = minPercent;
        }

        if (_maxPercentTextBox != null && decimal.TryParse(_maxPercentTextBox.Text, out var maxPercent))
        {
            condition.Parameters["MaRangeMax"] = maxPercent;
        }
    }

    private void SaveTradeVolumeParameters(ConditionItemViewModel condition)
    {
        if (_minTradeVolumeTextBox != null && decimal.TryParse(_minTradeVolumeTextBox.Text, out var minVolume))
        {
            condition.Parameters["MinTradeVolume"] = minVolume;
        }
    }

    private void SavePriceComparisonParameters(ConditionItemViewModel condition)
    {
        // 캔들 타입 (일봉 고정, 추후 확장 가능)
        condition.Parameters["CandleType"] = "Daily";

        // 비교 요소 A 파싱 (예: "0_Open" -> offset=0, element=Open)
        if (_priceElementAComboBox?.SelectedItem is ComboBoxItem elementAItem)
        {
            var tagA = elementAItem.Tag?.ToString() ?? "0_Close";
            var partsA = tagA.Split('_');
            if (partsA.Length == 2 && int.TryParse(partsA[0], out var offsetA))
            {
                condition.Parameters["CandleOffsetA"] = offsetA;
                condition.Parameters["PriceElementA"] = partsA[1]; // "Open", "High", "Low", "Close"
            }
        }

        // 비교 연산자
        if (_comparisonOperatorComboBox?.SelectedItem is ComboBoxItem opItem)
        {
            var op = opItem.Tag?.ToString() ?? ">";
            condition.Parameters["Operator"] = op switch
            {
                ">" => "GreaterThan",
                "<" => "LessThan",
                ">=" => "GreaterThanOrEquals",
                "<=" => "LessThanOrEquals",
                "=" => "Equals",
                _ => "GreaterThan"
            };
        }

        // 비교 요소 B 파싱
        if (_priceElementBComboBox?.SelectedItem is ComboBoxItem elementBItem)
        {
            var tagB = elementBItem.Tag?.ToString() ?? "1_Low";
            var partsB = tagB.Split('_');
            if (partsB.Length == 2 && int.TryParse(partsB[0], out var offsetB))
            {
                condition.Parameters["CandleOffsetB"] = offsetB;
                condition.Parameters["PriceElementB"] = partsB[1];
            }
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void LoadExistingCondition(ConditionItemViewModel condition)
    {
        if (condition == null) return;

        // 1. 조건 타입 ComboBox 선택 (자동으로 폼 빌드)
        string conditionTypeTag = condition.Type.ToString();
        foreach (ComboBoxItem item in ConditionTypeComboBox.Items)
        {
            if (item.Tag?.ToString() == conditionTypeTag)
            {
                ConditionTypeComboBox.SelectedItem = item;
                break;
            }
        }

        // 2. 폼이 빌드될 때까지 대기 (SelectionChanged 이벤트 완료 후)
        Dispatcher.InvokeAsync(() =>
        {
            // 3. 조건 타입별 파라미터 로드
            switch (conditionTypeTag)
            {
                case "PriceChange":
                    LoadPriceChangeParameters(condition);
                    break;
                case "MovingAverage":
                    LoadMovingAverageParameters(condition);
                    break;
                case "TradeVolume":
                    LoadTradeVolumeParameters(condition);
                    break;
                case "PriceComparison":
                    LoadPriceComparisonParameters(condition);
                    break;
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void LoadPriceChangeParameters(ConditionItemViewModel condition)
    {
        // 캔들 타입 로드
        if (condition.Parameters.TryGetValue("CandleType", out var candleType) && _candleTypeComboBox != null)
        {
            var candleTypeStr = candleType.ToString();
            foreach (ComboBoxItem item in _candleTypeComboBox.Items)
            {
                if (item.Tag?.ToString() == candleTypeStr)
                {
                    _candleTypeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        // 등락률 범위 로드
        if (condition.Parameters.TryGetValue("MinPercent", out var minPercent) && _minPercentTextBox != null)
        {
            _minPercentTextBox.Text = minPercent.ToString();
        }

        if (condition.Parameters.TryGetValue("MaxPercent", out var maxPercent) && _maxPercentTextBox != null)
        {
            _maxPercentTextBox.Text = maxPercent.ToString();
        }
    }

    private void LoadMovingAverageParameters(ConditionItemViewModel condition)
    {
        // MA 타입 로드 (10, 20, or 120)
        if (condition.Parameters.TryGetValue("MaType", out var maType) && _maTypeComboBox != null)
        {
            var maTypeStr = maType.ToString();
            foreach (ComboBoxItem item in _maTypeComboBox.Items)
            {
                if (item.Tag?.ToString() == maTypeStr)
                {
                    _maTypeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        // MA 범위 로드
        if (condition.Parameters.TryGetValue("MaRangeMin", out var maRangeMin) && _minPercentTextBox != null)
        {
            _minPercentTextBox.Text = maRangeMin.ToString();
        }

        if (condition.Parameters.TryGetValue("MaRangeMax", out var maRangeMax) && _maxPercentTextBox != null)
        {
            _maxPercentTextBox.Text = maRangeMax.ToString();
        }
    }

    private void LoadTradeVolumeParameters(ConditionItemViewModel condition)
    {
        // 최소 거래량 로드
        if (condition.Parameters.TryGetValue("MinTradeVolume", out var minVolume) && _minTradeVolumeTextBox != null)
        {
            _minTradeVolumeTextBox.Text = minVolume.ToString();
        }
    }

    private void LoadPriceComparisonParameters(ConditionItemViewModel condition)
    {
        // 비교 요소 A 로드 (예: offset=0, element=Open -> "0_Open")
        if (condition.Parameters.TryGetValue("CandleOffsetA", out var offsetA) &&
            condition.Parameters.TryGetValue("PriceElementA", out var elementA) &&
            _priceElementAComboBox != null)
        {
            var tagA = $"{offsetA}_{elementA}";
            foreach (ComboBoxItem item in _priceElementAComboBox.Items)
            {
                if (item.Tag?.ToString() == tagA)
                {
                    _priceElementAComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        // 비교 연산자 로드
        if (condition.Parameters.TryGetValue("Operator", out var operatorValue) && _comparisonOperatorComboBox != null)
        {
            var operatorStr = operatorValue.ToString();
            var operatorSymbol = operatorStr switch
            {
                "GreaterThan" => ">",
                "LessThan" => "<",
                "GreaterThanOrEquals" => ">=",
                "LessThanOrEquals" => "<=",
                "Equals" => "=",
                _ => ">"
            };

            foreach (ComboBoxItem item in _comparisonOperatorComboBox.Items)
            {
                if (item.Tag?.ToString() == operatorSymbol)
                {
                    _comparisonOperatorComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        // 비교 요소 B 로드
        if (condition.Parameters.TryGetValue("CandleOffsetB", out var offsetB) &&
            condition.Parameters.TryGetValue("PriceElementB", out var elementB) &&
            _priceElementBComboBox != null)
        {
            var tagB = $"{offsetB}_{elementB}";
            foreach (ComboBoxItem item in _priceElementBComboBox.Items)
            {
                if (item.Tag?.ToString() == tagB)
                {
                    _priceElementBComboBox.SelectedItem = item;
                    break;
                }
            }
        }
    }
}
