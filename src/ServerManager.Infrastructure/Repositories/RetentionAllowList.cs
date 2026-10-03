using System.Text.RegularExpressions;
using ServerManager.Application.Monitoring;

namespace ServerManager.Infrastructure.Repositories;

/// <summary>
/// Otomatik silme yalnızca geçici izleme tablolarında çalışabilir; kullanıcı ve sunucu verisi asla silinmez.
/// </summary>
internal static class RetentionAllowList
{
    private static readonly Regex AllowedTablePattern = new(
        "^(ServerMetrics|ServerMetricsHourly|ServerHealthChecks)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedColumnPattern = new(
        "^(CollectedAt|HourStart|CheckedAt)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<RetentionTarget, (string Table, string TimeColumn)> Targets =
        new Dictionary<RetentionTarget, (string, string)>
        {
            [RetentionTarget.RawMetrics] = ("ServerMetrics", "CollectedAt"),
            [RetentionTarget.HourlyMetrics] = ("ServerMetricsHourly", "HourStart"),
            [RetentionTarget.HealthChecks] = ("ServerHealthChecks", "CheckedAt")
        };

    public static (string Table, string TimeColumn) Resolve(RetentionTarget target)
    {
        if (!Targets.TryGetValue(target, out var mapping))
            throw new InvalidOperationException($"Bilinmeyen saklama hedefi: {target}. Silme işlemi durduruldu.");

        EnsureAllowed(mapping.Table, mapping.TimeColumn);
        return mapping;
    }

    public static void EnsureAllowed(string table, string timeColumn)
    {
        if (!AllowedTablePattern.IsMatch(table))
            throw new InvalidOperationException($"'{table}' tablosu silme izin listesinde değil. Silme işlemi durduruldu.");

        if (!AllowedColumnPattern.IsMatch(timeColumn))
            throw new InvalidOperationException($"'{timeColumn}' kolonu silme izin listesinde değil. Silme işlemi durduruldu.");
    }
}
