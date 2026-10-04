using System.Globalization;
using ServerManager.Application.DTOs.Settings;

namespace ServerManager.Web.Models;

public static class SettingFieldChoices
{
    public static IReadOnlyList<(string Id, string Label)> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones()
        .Select(zone => (zone.Id, Label(zone)))
        .OrderBy(zone => zone.Item2, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    public static bool IsPercent(PanelSettingFieldDto field) =>
        field.Key.EndsWith("Percent", StringComparison.Ordinal);

    public static IReadOnlyList<string> Percents(PanelSettingFieldDto field)
    {
        var minimum = (int)Math.Ceiling(field.Minimum ?? 0);
        var maximum = (int)Math.Floor(field.Maximum ?? 100);
        var start = ((minimum + 4) / 5) * 5;
        var values = new SortedSet<double>();
        for (var value = start; value <= maximum; value += 5)
            values.Add(value);

        if (double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var current)
            && current >= minimum && current <= maximum)
            values.Add(current);

        return values.Select(value => value.ToString("0.##", CultureInfo.InvariantCulture)).ToList();
    }

    private static string Label(TimeZoneInfo zone)
    {
        var offset = zone.BaseUtcOffset;
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        return $"(UTC{sign}{offset.Hours:00}:{offset.Minutes:00}) {zone.Id}";
    }
}
