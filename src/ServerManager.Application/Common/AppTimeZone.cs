namespace ServerManager.Application.Common;

public static class AppTimeZone
{
    private const string TimeZoneId = "Europe/Istanbul";

    private static readonly TimeZoneInfo Zone = ResolveZone();

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
