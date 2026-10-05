using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Backups;

public class BackupScheduleTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Hourly_aligns_to_the_full_hour()
    {
        var next = BackupSchedule.NextRun(BackupScheduleType.Hourly, 6, 0, null, Utc(2026, 3, 10, 10, 42), Istanbul);

        Assert.Equal(Utc(2026, 3, 10, 16), next);
    }

    [Fact]
    public void Daily_runs_today_when_the_time_has_not_passed()
    {
        // İstanbul UTC+3: 03:00 yerel = 00:00 UTC.
        var next = BackupSchedule.NextRun(BackupScheduleType.Daily, 0, 3 * 60, null, Utc(2026, 3, 9, 23, 30), Istanbul);

        Assert.Equal(Utc(2026, 3, 10, 0), next);
    }

    [Fact]
    public void Daily_moves_to_tomorrow_when_the_time_has_passed()
    {
        var next = BackupSchedule.NextRun(BackupScheduleType.Daily, 0, 3 * 60, null, Utc(2026, 3, 10, 0), Istanbul);

        Assert.Equal(Utc(2026, 3, 11, 0), next);
    }

    [Fact]
    public void Weekly_picks_the_requested_day()
    {
        // 2026-03-10 salı.
        var next = BackupSchedule.NextRun(BackupScheduleType.Weekly, 0, 4 * 60, DayOfWeek.Sunday, Utc(2026, 3, 10, 12), Istanbul);

        Assert.Equal(Utc(2026, 3, 15, 1), next);
        Assert.Equal(DayOfWeek.Sunday, TimeZoneInfo.ConvertTimeFromUtc(next!.Value, Istanbul).DayOfWeek);
    }

    [Fact]
    public void Weekly_on_the_same_day_after_the_time_moves_a_week_ahead()
    {
        var next = BackupSchedule.NextRun(BackupScheduleType.Weekly, 0, 4 * 60, DayOfWeek.Tuesday, Utc(2026, 3, 10, 12), Istanbul);

        Assert.Equal(Utc(2026, 3, 17, 1), next);
    }

    [Fact]
    public void Nonexistent_local_time_at_spring_forward_is_shifted_one_hour()
    {
        // Berlin 2026-03-29 02:00 → 03:00 (02:30 yok). 03:30 CEST = 01:30 UTC.
        var next = BackupSchedule.NextRun(BackupScheduleType.Daily, 0, 2 * 60 + 30, null, Utc(2026, 3, 28, 12), Berlin);

        Assert.Equal(Utc(2026, 3, 29, 1, 30), next);
    }

    [Fact]
    public void Local_time_is_kept_across_daylight_saving_change()
    {
        var beforeChange = BackupSchedule.NextRun(BackupScheduleType.Daily, 0, 4 * 60, null, Utc(2026, 3, 27, 12), Berlin);
        var afterChange = BackupSchedule.NextRun(BackupScheduleType.Daily, 0, 4 * 60, null, Utc(2026, 3, 29, 12), Berlin);

        Assert.Equal(Utc(2026, 3, 28, 3), beforeChange);
        Assert.Equal(Utc(2026, 3, 30, 2), afterChange);
    }

    [Fact]
    public void Manual_schedule_has_no_next_run()
    {
        Assert.Null(BackupSchedule.NextRun(BackupScheduleType.Manual, 1, 0, null, Utc(2026, 3, 10, 0), Istanbul));
    }

    [Fact]
    public void Unknown_time_zone_falls_back_to_local()
    {
        Assert.Equal(AppTimeZone.Default, BackupSchedule.ResolveTimeZone("Yok/Boyle-Bir-Yer"));
        Assert.Equal(AppTimeZone.Default, BackupSchedule.ResolveTimeZone(null));
        Assert.Equal(Istanbul.Id, BackupSchedule.ResolveTimeZone("Europe/Istanbul").Id);
    }
}
