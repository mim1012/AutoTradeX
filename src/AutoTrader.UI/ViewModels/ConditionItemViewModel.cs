using System;
using System.Collections.Generic;
using AutoTrader.UI.Models;

namespace AutoTrader.UI.ViewModels;

/// <summary>
/// 조건 항목 ViewModel (체크박스 리스트의 각 항목)
/// </summary>
public class ConditionItemViewModel : ViewModelBase
{
    private string _id = string.Empty;
    private bool _isEnabled = true;
    private ConditionType _type;
    private string _description = string.Empty;
    private bool _isConditionMet;
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 조건 ID (A, B, C, ...)
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// 활성화 여부 (체크박스)
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// 조건 타입
    /// </summary>
    public ConditionType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    /// <summary>
    /// 조건 설명 텍스트 (예: "등락률: [일봉] 0봉 전 종가 대비 -7.0% ~ 0.0%")
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// 실시간 조건 충족 여부
    /// </summary>
    public bool IsConditionMet
    {
        get => _isConditionMet;
        set => SetProperty(ref _isConditionMet, value);
    }

    /// <summary>
    /// 상태 메시지 (예: "✅ 충족 (TSLA: -5.2%)")
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// 조건 파라미터 (조건 타입별로 다름)
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// 조건 ID (Alias for Id, for compatibility with ConditionSetEditor)
    /// </summary>
    public string ConditionId
    {
        get => Id;
        set => Id = value;
    }

    /// <summary>
    /// 조건 타입 (Alias for Type, for compatibility with ConditionSetEditor)
    /// </summary>
    public string ConditionType
    {
        get => Type.ToString();
        set
        {
            if (Enum.TryParse<ConditionType>(value, out var type))
            {
                Type = type;
            }
        }
    }
}
