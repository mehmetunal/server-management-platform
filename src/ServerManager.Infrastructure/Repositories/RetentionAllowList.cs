using System.Text.RegularExpressions;
using ServerManager.Application.Monitoring;

namespace ServerManager.Infrastructure.Repositories;

/// <summary>
/// Otomatik silme yalnızca geçici izleme tablolarında çalışabilir; kullanıcı ve sunucu verisi asla silinmez.
/// </summary>
internal static class RetentionAllowList
{
    private static readonly Regex AllowedTablePattern = new(
        "^(ServerMetrics|ServerMetricsHourly|ServerHealthChecks|UptimeCheckResults|NotificationDeliveries)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedColumnPattern = new(
        "^(CollectedAt|HourStart|CheckedAt|SentAt)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedPartitionPattern = new(
        "^(ServerId|CheckId|ChannelId)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<RetentionTarget, (string Table, string TimeColumn, string PartitionColumn)> Targets =
        new Dictionary<RetentionTarget, (string, string, string)>
        {
            [RetentionTarget.RawMetrics] = ("ServerMetrics", "CollectedAt", "ServerId"),
            [RetentionTarget.HourlyMetrics] = ("ServerMetricsHourly", "HourStart", "ServerId"),
            [RetentionTarget.HealthChecks] = ("ServerHealthChecks", "CheckedAt", "ServerId"),
            [RetentionTarget.UptimeResults] = ("UptimeCheckResults", "CheckedAt", "CheckId"),
            [RetentionTarget.NotificationDeliveries] = ("NotificationDeliveries", "SentAt", "ChannelId")
        };

    public static (string Table, string TimeColumn, string PartitionColumn) Resolve(RetentionTarget target)
    {
        if (!Targets.TryGetValue(target, out var mapping))
            throw new InvalidOperationException($"Bilinmeyen saklama hedefi: {target}. Silme işlemi durduruldu.");

        EnsureAllowed(mapping.Table, mapping.TimeColumn, mapping.PartitionColumn);
        return mapping;
    }

    public static void EnsureAllowed(string table, string timeColumn, string partitionColumn = "ServerId")
    {
        if (!AllowedTablePattern.IsMatch(table))
            throw new InvalidOperationException($"'{table}' tablosu silme izin listesinde değil. Silme işlemi durduruldu.");

        if (!AllowedColumnPattern.IsMatch(timeColumn))
            throw new InvalidOperationException($"'{timeColumn}' kolonu silme izin listesinde değil. Silme işlemi durduruldu.");

        if (!AllowedPartitionPattern.IsMatch(partitionColumn))
            throw new InvalidOperationException($"'{partitionColumn}' kolonu silme izin listesinde değil. Silme işlemi durduruldu.");
    }
}
