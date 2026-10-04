using ServerManager.Domain.Enums;

namespace ServerManager.Application.Backups;

public static class BackupSchedule
{
    public const int MinIntervalHours = 1;
    public const int MaxIntervalHours = 168;
    public const int MinutesPerDay = 1440;

    /// <summary>
    /// <paramref name="afterUtc"/>'den sonraki ilk çalışma zamanı (UTC). Saatlik zamanlama tam saate hizalanır;
    /// günlük/haftalık saat <paramref name="timeZone"/>'a göre yorumlanır. Yaz saatine geçişte var olmayan saat bir saat ileri alınır.
    /// </summary>
    public static DateTime? NextRun(
        BackupScheduleType type,
        int intervalHours,
        int minuteOfDay,
        DayOfWeek? dayOfWeek,
        DateTime afterUtc,
        TimeZoneInfo timeZone)
    {
        afterUtc = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        switch (type)
        {
            case BackupScheduleType.Hourly:
                var hours = Math.Clamp(intervalHours, MinIntervalHours, MaxIntervalHours);
                var hour = new DateTime(afterUtc.Year, afterUtc.Month, afterUtc.Day, afterUtc.Hour, 0, 0, DateTimeKind.Utc);
                return hour.AddHours(hours);

            case BackupScheduleType.Daily:
            case BackupScheduleType.Weekly:
                var minutes = Math.Clamp(minuteOfDay, 0, MinutesPerDay - 1);
                var localDate = TimeZoneInfo.ConvertTimeFromUtc(afterUtc, timeZone).Date;
                for (var offset = 0; offset <= 8; offset++)
                {
                    var date = localDate.AddDays(offset);
                    if (type == BackupScheduleType.Weekly && date.DayOfWeek != (dayOfWeek ?? DayOfWeek.Sunday))
                        continue;

                    var candidate = ToUtc(date.AddMinutes(minutes), timeZone);
                    if (candidate > afterUtc)
                        return candidate;
                }

                return null;

            default:
                return null;
        }
    }

    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            return zone;

        return TimeZoneInfo.Local;
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
            local = local.AddHours(1);

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
