using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AutoTrader.UI.Commands;
using AutoTrader.UI.Models;

namespace AutoTrader.UI.ViewModels;

/// <summary>
/// 조건식 빌더 ViewModel
/// </summary>
public class ConditionBuilderViewModel : ViewModelBase
{
    private string _logicExpression = string.Empty;
    private string _logicExplanation = string.Empty;

    public ConditionBuilderViewModel()
    {
        Conditions = new ObservableCollection<ConditionItemViewModel>();
        
        // Commands 초기화
        AddConditionCommand = new RelayCommand(ExecuteAddCondition, CanExecuteAddCondition);
        EditConditionCommand = new RelayCommand<ConditionItemViewModel>(ExecuteEditCondition);
        RemoveConditionCommand = new RelayCommand<ConditionItemViewModel>(ExecuteRemoveCondition);
        SaveConditionsCommand = new RelayCommand(ExecuteSaveConditions);
        TestConditionsCommand = new RelayCommand(ExecuteTestConditions);
        
        // 초기 논리식 설정
        UpdateLogicExpression();
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
    }

    private void ExecuteEditCondition(ConditionItemViewModel? condition)
    {
        if (condition == null) return;
        
        // TODO: 조건 편집 다이얼로그 열기
    }

    private void ExecuteRemoveCondition(ConditionItemViewModel? condition)
    {
        if (condition == null) return;
        
        Conditions.Remove(condition);
        ReassignConditionIds();
        UpdateLogicExpression();
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

    #endregion
}
