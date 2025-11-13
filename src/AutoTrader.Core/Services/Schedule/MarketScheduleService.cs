using AutoTrader.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoTrader.Core.Services.Schedule;

/// <summary>
/// 미국 주식 시장 스케줄 관리 서비스
/// - DST(Daylight Saving Time) 자동 적용
/// - 정확한 시장 개장/폐장 시간 계산
/// - 한국 시간 ↔ 미국 동부 시간 변환
/// </summary>
public interface IMarketScheduleService
{
    /// <summary>현재 시장 상태 (Open/Closed)</summary>
    MarketStatus GetCurrentMarketStatus();

    /// <summary>현재 DST 적용 여부</summary>
    bool IsDaylightSavingTime();

    /// <summary>시장 마감까지 남은 시간</summary>
    TimeSpan GetTimeUntilMarketClose();

    /// <summary>시장 마감 시간 문자열 (ET 기준)</summary>
    string GetMarketCloseTimeString();

    /// <summary>현재 미국 동부 시간</summary>
    DateTime GetEasternTime();
}

public enum MarketStatus
{
    Closed,
    Open,
    PreMarket,
    AfterHours
}

public class MarketScheduleService : IMarketScheduleService
{
    private readonly ILogger<MarketScheduleService> _logger;

    // 미국 동부 표준시 (EST): UTC-5
    // 미국 동부 서머타임 (EDT): UTC-4
    private static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    public MarketScheduleService(ILogger<MarketScheduleService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 현재 미국 동부 시간 계산 (DST 자동 적용)
    /// </summary>
    public DateTime GetEasternTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternTimeZone);
    }

    /// <summary>
    /// 현재 DST(Daylight Saving Time) 적용 여부
    /// - 3월 두 번째 일요일 02:00 ~ 11월 첫 번째 일요일 02:00
    /// </summary>
    public bool IsDaylightSavingTime()
    {
        return EasternTimeZone.IsDaylightSavingTime(DateTime.UtcNow);
    }

    /// <summary>
    /// 현재 시장 상태
    /// - 정규장: 09:30 ~ 16:00 ET
    /// - 프리마켓: 04:00 ~ 09:30 ET
    /// - 애프터아워: 16:00 ~ 20:00 ET
    /// </summary>
    public MarketStatus GetCurrentMarketStatus()
    {
        var et = GetEasternTime();
        var now = et.TimeOfDay;

        // 주말 체크
        if (et.DayOfWeek == DayOfWeek.Saturday || et.DayOfWeek == DayOfWeek.Sunday)
            return MarketStatus.Closed;

        // 정규장: 09:30 ~ 16:00
        if (now >= new TimeSpan(9, 30, 0) && now < new TimeSpan(16, 0, 0))
            return MarketStatus.Open;

        // 프리마켓: 04:00 ~ 09:30
        if (now >= new TimeSpan(4, 0, 0) && now < new TimeSpan(9, 30, 0))
            return MarketStatus.PreMarket;

        // 애프터아워: 16:00 ~ 20:00
        if (now >= new TimeSpan(16, 0, 0) && now < new TimeSpan(20, 0, 0))
            return MarketStatus.AfterHours;

        return MarketStatus.Closed;
    }

    /// <summary>
    /// 시장 마감까지 남은 시간
    /// </summary>
    public TimeSpan GetTimeUntilMarketClose()
    {
        var et = GetEasternTime();
        var closeTime = new DateTime(et.Year, et.Month, et.Day, 16, 0, 0);

        // 이미 마감된 경우 다음 영업일
        if (et.TimeOfDay >= new TimeSpan(16, 0, 0))
        {
            closeTime = closeTime.AddDays(1);

            // 금요일이면 월요일로
            if (et.DayOfWeek == DayOfWeek.Friday)
                closeTime = closeTime.AddDays(2);
            // 토요일이면 월요일로
            else if (et.DayOfWeek == DayOfWeek.Saturday)
                closeTime = closeTime.AddDays(1);
        }
        // 주말인 경우 월요일로
        else if (et.DayOfWeek == DayOfWeek.Saturday)
        {
            closeTime = closeTime.AddDays(2);
        }
        else if (et.DayOfWeek == DayOfWeek.Sunday)
        {
            closeTime = closeTime.AddDays(1);
        }

        return closeTime - et;
    }

    /// <summary>
    /// 시장 마감 시간 문자열 (ET 기준)
    /// </summary>
    public string GetMarketCloseTimeString()
    {
        var isDst = IsDaylightSavingTime();
        return isDst ? "16:00 EDT" : "16:00 EST";
    }
}
