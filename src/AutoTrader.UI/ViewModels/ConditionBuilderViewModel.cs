using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using AutoTrader.UI.Commands;
using AutoTrader.UI.Models;
using AutoTrader.UI.Services;
using AutoTrader.Core.Services.Trading;
using AutoTrader.Core.Models.Trading;

namespace AutoTrader.UI.ViewModels;

/// <summary>
/// 조건식 빌더 ViewModel
/// </summary>
public class ConditionBuilderViewModel : ViewModelBase
{
    private string _logicExpression = string.Empty;
    private string _logicExplanation = string.Empty;
    private readonly ConditionMappingService _mappingService;
    private readonly IConditionEvaluator? _conditionEvaluator;

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
            // TODO: 조건 추가 다이얼로그 열기
            // 임시로 샘플 조건 추가
            var newId = GetNextConditionId();
            var newCondition = new ConditionItemViewModel
            {
                Id = newId,
                Type = ConditionType.PriceChange,
                Description = "등락률: [일봉] 0봉 전 종가 대비 -7.0% ~ 0.0%",
                IsEnabled = true,
                IsConditionMet = false,
                StatusMessage = "⏳ 평가 대기 중"
            };

            Conditions.Add(newCondition);
            UpdateLogicExpression();

            // Command CanExecute 재평가
            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            // TODO: 로깅 서비스 추가 후 로그 기록
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to add condition: {ex.Message}");
            // TODO: 사용자에게 에러 메시지 표시
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

    private void ExecuteSaveConditions()
    {
        // TODO: 조건식 저장 로직 구현
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

        // 논리식 생성 (기본: AND 연산)
        LogicExpression = string.Join(" and ", enabledConditions.Select(c => c.Id));
        
        // 설명 생성
        if (enabledConditions.Count == 1)
        {
            LogicExplanation = $"조건 {enabledConditions[0].Id}가 충족되어야 합니다.";
        }
        else
        {
            var ids = string.Join(", ", enabledConditions.Select(c => c.Id));
            LogicExplanation = $"조건 {ids}가 모두 충족되어야 합니다.";
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
