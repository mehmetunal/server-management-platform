using ServerManager.Application.Common;

namespace ServerManager.Web.Helpers;

public static class DateDisplay
{
    public const string DefaultFormat = "dd.MM.yyyy HH:mm";

    public static string Format(DateTime? utc, string format = DefaultFormat, string empty = "—") =>
        utc.HasValue ? AppTimeZone.ToLocal(utc.Value).ToString(format) : empty;

    public static string Relative(DateTime? utc, string empty = "Hiç")
    {
        if (!utc.HasValue)
            return empty;

        var elapsed = DateTime.UtcNow - DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
        if (elapsed.TotalSeconds < 60)
            return "az önce";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes} dk önce";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours} saat önce";
        if (elapsed.TotalDays < 30)
            return $"{(int)elapsed.TotalDays} gün önce";

        return Format(utc, "dd.MM.yyyy");
    }
}
