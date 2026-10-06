using System.Globalization;

namespace ServerManager.Web.Helpers;

public static class MetricDisplay
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Percent(double? value) =>
        value.HasValue ? "%" + value.Value.ToString("0.#", Turkish) : "—";

    public static string Bytes(long? bytes)
    {
        if (!bytes.HasValue)
            return "—";

        double value = bytes.Value;
        var unit = 0;
        while (Math.Abs(value) >= 1024 && unit < ByteUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(unit == 0 ? "0" : "0.#", Turkish) + " " + ByteUnits[unit];
    }

    public static string Rate(double? bytesPerSecond) =>
        bytesPerSecond.HasValue ? Bytes((long)Math.Round(bytesPerSecond.Value)) + "/s" : "—";

    /// <summary>"3,2 GB / 8 GB" biçiminde kullanılan / toplam; toplam bilinmiyorsa "—".</summary>
    public static string UsedOfTotal(long used, long total) =>
        total > 0 ? $"{Bytes(used)} / {Bytes(total)}" : "—";

    /// <summary>CPU yüzdesini kullanılan çekirdek eşdeğerine çevirir: "%40, 4 thread" → "1,6 / 4 çekirdek".</summary>
    public static string CpuCores(double? percent, int? threads) =>
        percent.HasValue && threads is > 0
            ? $"{(percent.Value * threads.Value / 100).ToString("0.#", Turkish)} / {threads.Value} çekirdek"
            : "—";

    public static string Number(double? value, string format = "0.##") =>
        value.HasValue ? value.Value.ToString(format, Turkish) : "—";

    public static string Uptime(long? seconds)
    {
        if (!seconds.HasValue || seconds.Value <= 0)
            return "—";

        var span = TimeSpan.FromSeconds(seconds.Value);
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays} gün {span.Hours} sa";
        if (span.TotalHours >= 1)
            return $"{span.Hours} sa {span.Minutes} dk";
        return $"{Math.Max(1, span.Minutes)} dk";
    }

    public static string UsageBarClass(double? percent, double warning, double critical) => percent switch
    {
        null => "bg-slate-300 dark:bg-slate-700",
        _ when percent >= critical => "bg-red-500",
        _ when percent >= warning => "bg-amber-500",
        _ => "bg-emerald-500"
    };

    public static string BarWidth(double? percent) =>
        Math.Clamp(percent ?? 0, 0, 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}
