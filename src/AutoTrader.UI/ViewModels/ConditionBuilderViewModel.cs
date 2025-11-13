using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using AutoTrader.UI.Commands;
using AutoTrader.UI.Models;
using AutoTrader.UI.Services;
using AutoTrader.Core.Services.Trading;
using AutoTrader.Core.Models.Trading;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Models.Database;
using System.Text.Json;

namespace AutoTrader.UI.ViewModels;

/// <summary>
/// 조건식 빌더 ViewModel
/// </summary>
public class ConditionBuilderViewModel : ViewModelBase
{
    private string _logicExpression = string.Empty;
    private string _logicExplanation = string.Empty;
    private string _logicOperator = "AND"; // 기본값: AND
    private readonly ConditionMappingService _mappingService;
    private readonly IConditionEvaluator? _conditionEvaluator;
    private readonly AccountRepository? _accountRepository;
    private readonly ConditionSetRepository? _conditionSetRepository;

    public ConditionBuilderViewModel()
    {
        _mappingService = new ConditionMappingService();
        Conditions = new ObservableCollection<ConditionItemViewModel>();

        // Conditions 컬렉션 변경 이벤트 구독
        Conditions.CollectionChanged += OnConditionsCollectionChanged;

        // Commands 초기화
        AddConditionCommand = new RelayCommand(_ => ExecuteAddCondition(), _ => CanExecuteAddCondition());
        EditConditionCommand = new RelayCommand<ConditionItemViewModel>(ExecuteEditCondition);
        RemoveConditionCommand = new RelayCommand<ConditionItemViewModel>(ExecuteRemoveCondition);
        SaveConditionsCommand = new RelayCommand(_ => ExecuteSaveConditions());
        TestConditionsCommand = new RelayCommand(_ => ExecuteTestConditions());

        // 초기 논리식 설정
        UpdateLogicExpression();
    }

    public ConditionBuilderViewModel(IConditionEvaluator conditionEvaluator) : this()
    {
        _conditionEvaluator = conditionEvaluator;
    }

    public ConditionBuilderViewModel(AccountRepository accountRepository, ConditionSetRepository conditionSetRepository) : this()
    {
        _accountRepository = accountRepository;
        _conditionSetRepository = conditionSetRepository;

        // 저장된 조건식 로드
        _ = LoadConditionsAsync();
    }

    /// <summary>
    /// 활성 계좌의 저장된 조건식 로드
    /// </summary>
    private async Task LoadConditionsAsync()
    {
        if (_accountRepository == null || _conditionSetRepository == null)
            return;

        try
        {
            var activeAccount = await _accountRepository.GetActiveAccountAsync();
            if (activeAccount == null)
                return;

            var conditionSet = await _conditionSetRepository.GetConditionSetByAccountIdAsync(activeAccount.AccountId);
            if (conditionSet == null || conditionSet.Conditions.Count == 0)
                return;

            // 기존 조건 클리어
            Conditions.Clear();

            // DB에서 불러온 조건들을 ViewModel에 추가
            foreach (var condition in conditionSet.Conditions.OrderBy(c => c.ConditionOrder))
            {
                var conditionViewModel = DeserializeCondition(condition);
                if (conditionViewModel != null)
                {
                    Conditions.Add(conditionViewModel);

                    // 첫 번째 조건의 LogicOperator를 기준으로 설정
                    if (!string.IsNullOrEmpty(condition.LogicOperator))
                    {
                        LogicOperator = condition.LogicOperator.ToUpper();
                    }
                }
            }

            UpdateLogicExpression();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load conditions: {ex.Message}");
        }
    }

    /// <summary>
    /// DB Condition을 ConditionItemViewModel로 변환
    /// </summary>
    private ConditionItemViewModel? DeserializeCondition(AutoTrader.Core.Models.Database.Condition condition)
    {
        try
        {
            var parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(condition.Parameters);
            if (parameters == null)
                return null;

            var description = parameters.ContainsKey("Description")
                ? parameters["Description"].GetString() ?? "조건"
                : "조건";

            var typeString = parameters.ContainsKey("Type")
                ? parameters["Type"].GetString()
                : condition.ConditionType;

            if (!Enum.TryParse<Models.ConditionType>(typeString, out var conditionType))
                conditionType = Models.ConditionType.PriceChange;

            return new ConditionItemViewModel
            {
                Id = ((char)('A' + condition.ConditionOrder - 1)).ToString(),
                Type = conditionType,
                Description = description,
                IsEnabled = true,
                IsConditionMet = false,
                StatusMessage = "⏳ 평가 대기 중"
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to deserialize condition: {ex.Message}");
            return null;
        }
    }

    #region Properties

    /// <summary>
    /// 조건 목록 (최대 3개)
    /// </summary>
    public ObservableCollection<ConditionItemViewModel> Conditions { get; }

    /// <summary>
    /// 조건식 텍스트 (예: "A and B")
    /// </summary>
    public string LogicExpression
    {
        get => _logicExpression;
        set => SetProperty(ref _logicExpression, value);
    }

    /// <summary>
    /// 조건식 설명 텍스트
    /// </summary>
    public string LogicExplanation
    {
        get => _logicExplanation;
        set => SetProperty(ref _logicExplanation, value);
    }

    /// <summary>
    /// 논리 연산자 (AND 또는 OR)
    /// </summary>
    public string LogicOperator
    {
        get => _logicOperator;
        set
        {
            if (SetProperty(ref _logicOperator, value))
            {
                UpdateLogicExpression();
            }
        }
    }

    #endregion

    #region Commands

    public ICommand AddConditionCommand { get; }
    public ICommand EditConditionCommand { get; }
    public ICommand RemoveConditionCommand { get; }
    public ICommand SaveConditionsCommand { get; }
    public ICommand TestConditionsCommand { get; }

    #endregion

    #region Command Handlers

    private bool CanExecuteAddCondition()
    {
        // 최대 3개까지만 추가 가능
        return Conditions.Count < 3;
    }

    private void ExecuteAddCondition()
    {
        try
        {
            // 조건 추가 다이얼로그 열기
            var dialog = new Views.ConditionEditorDialog();

            // Owner를 현재 활성 윈도우로 설정
            foreach (Window window in Application.Current.Windows)
            {
                if (window.IsActive && window is Views.ConditionBuilderDialog)
                {
                    dialog.Owner = window;
                    break;
                }
            }

            if (dialog.ShowDialog() == true && dialog.ResultCondition != null)
            {
                var newId = GetNextConditionId();
                var newCondition = dialog.ResultCondition;
                newCondition.Id = newId;
                newCondition.IsConditionMet = false;
                newCondition.StatusMessage = "⏳ 평가 대기 중";

                Conditions.Add(newCondition);
                UpdateLogicExpression();

                // Command CanExecute 재평가
                CommandManager.InvalidateRequerySuggested();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to add condition: {ex.Message}");
            MessageBox.Show($"조건 추가 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteEditCondition(ConditionItemViewModel? condition)
    {
        if (condition == null) return;
        
        // TODO: 조건 편집 다이얼로그 열기
    }

    private void ExecuteRemoveCondition(ConditionItemViewModel? condition)
    {
        if (condition == null) return;

        try
        {
            Conditions.Remove(condition);
            ReassignConditionIds();
            UpdateLogicExpression();

            // Command CanExecute 재평가
            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            // TODO: 로깅 서비스 추가 후 로그 기록
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to remove condition: {ex.Message}");
            // TODO: 사용자에게 에러 메시지 표시
        }
    }

    private async void ExecuteSaveConditions()
    {
        if (_accountRepository == null || _conditionSetRepository == null)
        {
            MessageBox.Show("Repository가 초기화되지 않았습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            // 활성 계좌 가져오기
            var activeAccount = await _accountRepository.GetActiveAccountAsync();
            if (activeAccount == null)
            {
                MessageBox.Show("활성 계좌를 먼저 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 조건이 없으면 저장 안함
            if (Conditions.Count == 0)
            {
                MessageBox.Show("저장할 조건이 없습니다. 조건을 추가해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 조건 목록 생성
            var conditionList = new List<AutoTrader.Core.Models.Database.Condition>();
            int order = 1;
            var enabledConditionsCount = Conditions.Count(c => c.IsEnabled);

            foreach (var conditionViewModel in Conditions.Where(c => c.IsEnabled))
            {
                var condition = new AutoTrader.Core.Models.Database.Condition
                {
                    ConditionOrder = order,
                    ConditionType = conditionViewModel.Type.ToString(),
                    Parameters = SerializeConditionParameters(conditionViewModel),
                    LogicOperator = order < enabledConditionsCount ? LogicOperator.ToUpper() : null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                conditionList.Add(condition);
                order++;
            }

            // DB에 저장 (Upsert)
            var conditionSet = await _conditionSetRepository.UpsertConditionSetAsync(
                activeAccount.AccountId,
                $"{activeAccount.AccountName} 조건식",
                conditionList
            );

            MessageBox.Show(
                $"조건식이 저장되었습니다.\n\n" +
                $"계좌: {activeAccount.AccountNumber}\n" +
                $"조건 개수: {conditionSet.Conditions.Count}개",
                "저장 완료",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show($"조건식 저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string SerializeConditionParameters(ConditionItemViewModel condition)
    {
        // 조건 타입별로 파라미터를 JSON으로 직렬화
        // TODO: 실제 파라미터 값들을 포함한 JSON 생성
        // 현재는 Description만 저장
        var parameters = new
        {
            Description = condition.Description,
            Type = condition.Type.ToString()
        };
        return JsonSerializer.Serialize(parameters);
    }

    private void ExecuteTestConditions()
    {
        // TODO: 조건식 테스트 실행 로직 구현
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Conditions 컬렉션 변경 시 호출
    /// </summary>
    private void OnConditionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 새로 추가된 항목의 PropertyChanged 구독
        if (e.NewItems != null)
        {
            foreach (ConditionItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnConditionItemPropertyChanged;
            }
        }

        // 제거된 항목의 PropertyChanged 구독 해제
        if (e.OldItems != null)
        {
            foreach (ConditionItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnConditionItemPropertyChanged;
            }
        }
    }

    /// <summary>
    /// 개별 조건 항목의 속성 변경 시 호출
    /// </summary>
    private void OnConditionItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // IsEnabled 속성이 변경되면 논리식 업데이트
        if (e.PropertyName == nameof(ConditionItemViewModel.IsEnabled))
        {
            UpdateLogicExpression();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 다음 조건 ID 생성 (A, B, C, ...)
    /// </summary>
    private string GetNextConditionId()
    {
        if (Conditions.Count == 0) return "A";
        
        var lastId = Conditions.Last().Id;
        var nextChar = (char)(lastId[0] + 1);
        return nextChar.ToString();
    }

    /// <summary>
    /// 조건 삭제 후 ID 재할당
    /// </summary>
    private void ReassignConditionIds()
    {
        for (int i = 0; i < Conditions.Count; i++)
        {
            Conditions[i].Id = ((char)('A' + i)).ToString();
        }
    }

    /// <summary>
    /// 논리식 텍스트 업데이트
    /// </summary>
    private void UpdateLogicExpression()
    {
        if (Conditions.Count == 0)
        {
            LogicExpression = string.Empty;
            LogicExplanation = "조건을 추가해주세요.";
            return;
        }

        // 활성화된 조건만 필터링
        var enabledConditions = Conditions.Where(c => c.IsEnabled).ToList();
        
        if (enabledConditions.Count == 0)
        {
            LogicExpression = string.Empty;
            LogicExplanation = "활성화된 조건이 없습니다.";
            return;
        }

        // 논리식 생성 (AND 또는 OR 연산)
        var operatorText = LogicOperator.ToUpper() == "OR" ? " or " : " and ";
        LogicExpression = string.Join(operatorText, enabledConditions.Select(c => c.Id));

        // 설명 생성
        if (enabledConditions.Count == 1)
        {
            LogicExplanation = $"조건 {enabledConditions[0].Id}가 충족되어야 합니다.";
        }
        else
        {
            var ids = string.Join(", ", enabledConditions.Select(c => c.Id));
            if (LogicOperator.ToUpper() == "OR")
            {
                LogicExplanation = $"조건 {ids} 중 하나 이상이 충족되어야 합니다.";
            }
            else
            {
                LogicExplanation = $"조건 {ids}가 모두 충족되어야 합니다.";
            }
        }
    }

    /// <summary>
    /// UI 조건을 Core CompositeCondition으로 변환
    /// </summary>
    public CompositeCondition GetCompositeCondition()
    {
        var logic = ConditionLogic.And; // 기본값: AND 연산
        return _mappingService.MapToCompositeCondition(Conditions.ToList(), logic);
    }

    #endregion
}
