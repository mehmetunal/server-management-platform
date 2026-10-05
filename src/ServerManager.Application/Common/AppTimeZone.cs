namespace ServerManager.Application.Common;

public static class AppTimeZone
{
    private const string TimeZoneId = "Europe/Istanbul";

    private static readonly TimeZoneInfo Zone = ResolveZone();

    /// <summary>Panelin saat dilimi; sistemde bulunamazsa sabit +03:00 kullanılır.</summary>
    public static TimeZoneInfo Default => Zone;

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(TimeZoneId, TimeSpan.FromHours(3), TimeZoneId, TimeZoneId);
        }
    }
}
