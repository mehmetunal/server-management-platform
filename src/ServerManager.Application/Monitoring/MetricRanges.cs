namespace ServerManager.Application.Monitoring;

public static class MetricRanges
{
    public static readonly IReadOnlyList<MetricRange> All =
        [MetricRange.OneHour, MetricRange.SixHours, MetricRange.OneDay, MetricRange.SevenDays, MetricRange.ThirtyDays];

    public static MetricRange Parse(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "6h" => MetricRange.SixHours,
        "24h" => MetricRange.OneDay,
        "7d" => MetricRange.SevenDays,
        "30d" => MetricRange.ThirtyDays,
        _ => MetricRange.OneHour
    };

    public static string Code(MetricRange range) => range switch
    {
        MetricRange.SixHours => "6h",
        MetricRange.OneDay => "24h",
        MetricRange.SevenDays => "7d",
        MetricRange.ThirtyDays => "30d",
        _ => "1h"
    };

    public static string DisplayName(MetricRange range) => range switch
    {
        MetricRange.SixHours => "Son 6 saat",
        MetricRange.OneDay => "Son 24 saat",
        MetricRange.SevenDays => "Son 7 gün",
        MetricRange.ThirtyDays => "Son 30 gün",
        _ => "Son 1 saat"
    };

    public static TimeSpan Duration(MetricRange range) => range switch
    {
        MetricRange.SixHours => TimeSpan.FromHours(6),
        MetricRange.OneDay => TimeSpan.FromHours(24),
        MetricRange.SevenDays => TimeSpan.FromDays(7),
        MetricRange.ThirtyDays => TimeSpan.FromDays(30),
        _ => TimeSpan.FromHours(1)
    };

    public static bool UsesHourlyData(MetricRange range) =>
        range is MetricRange.SevenDays or MetricRange.ThirtyDays;

    public static int BucketSeconds(MetricRange range) => range switch
    {
        MetricRange.SixHours => 120,
        MetricRange.OneDay => 300,
        MetricRange.SevenDays => 3600,
        MetricRange.ThirtyDays => 3 * 3600,
        _ => 30
    };
}
