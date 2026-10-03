using System.Globalization;
using System.Text.RegularExpressions;

namespace ServerManager.Infrastructure.Docker;

internal static partial class DockerValueParser
{
    private static readonly Dictionary<string, double> SizeUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["b"] = 1,
        ["kb"] = 1e3,
        ["mb"] = 1e6,
        ["gb"] = 1e9,
        ["tb"] = 1e12,
        ["pb"] = 1e15,
        ["kib"] = 1024d,
        ["mib"] = 1024d * 1024,
        ["gib"] = 1024d * 1024 * 1024,
        ["tib"] = 1024d * 1024 * 1024 * 1024,
        ["pib"] = 1024d * 1024 * 1024 * 1024 * 1024
    };

    /// <summary>"62.4MB", "5.953MiB", "0B", "1.2GB (50%)" gibi Docker boyutlarını bayta çevirir; okunamazsa null.</summary>
    public static long? ParseSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = SizeRegex().Match(value.Trim());
        if (!match.Success
            || !double.TryParse(match.Groups["number"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || !SizeUnits.TryGetValue(match.Groups["unit"].Value, out var multiplier))
            return null;

        return (long)Math.Round(number * multiplier);
    }

    /// <summary>"5.9MiB / 7.6GiB" gibi çift değerleri ayrıştırır.</summary>
    public static (long First, long Second) ParseSizePair(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (0, 0);

        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        return (ParseSize(parts[0]) ?? 0, parts.Length > 1 ? ParseSize(parts[1]) ?? 0 : 0);
    }

    public static double ParsePercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var trimmed = value.Trim().TrimEnd('%');
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.IsFinite(result)
            ? result
            : 0;
    }

    public static int ParseInt(string? value) =>
        int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    public static int? ParseNullableInt(string? value) =>
        int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

    /// <summary>
    /// "2026-10-03 18:31:49 +0000 UTC", "2026-10-03 18:31:49.693094553 +0000 UTC" ve RFC3339 (nanosaniyeli) biçimlerini UTC'ye çevirir.
    /// Docker'ın sıfır değeri (0001-01-01) null döner.
    /// </summary>
    public static DateTime? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("0001-01-01", StringComparison.Ordinal))
            return null;

        var match = TimestampRegex().Match(value.Trim());
        if (!match.Success)
            return null;

        var fraction = match.Groups["fraction"].Value;
        if (fraction.Length > 7)
            fraction = fraction[..7];

        var offset = match.Groups["offset"].Value;
        if (offset is "" or "Z")
            offset = "+00:00";
        else if (offset.Length == 5)
            offset = offset[..3] + ":" + offset[3..];

        var normalized = $"{match.Groups["date"].Value}T{match.Groups["time"].Value}{(fraction.Length > 0 ? "." + fraction : string.Empty)}{offset}";
        return DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    /// <summary>"Exited (3) 2 minutes ago" durum metninden çıkış kodunu okur.</summary>
    public static int? ParseExitCode(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return null;

        var match = ExitCodeRegex().Match(status);
        return match.Success ? ParseNullableInt(match.Groups["code"].Value) : null;
    }

    /// <summary>"Up 5 minutes (healthy)" durum metninden sağlık durumunu okur.</summary>
    public static string? ParseHealth(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return null;

        if (status.Contains("(unhealthy)", StringComparison.Ordinal))
            return "unhealthy";
        if (status.Contains("(healthy)", StringComparison.Ordinal))
            return "healthy";
        if (status.Contains("(health: starting)", StringComparison.Ordinal))
            return "starting";
        return null;
    }

    [GeneratedRegex(@"^(?<number>\d+(?:\.\d+)?)\s*(?<unit>[a-zA-Z]+)")]
    private static partial Regex SizeRegex();

    [GeneratedRegex(@"^(?<date>\d{4}-\d{2}-\d{2})[T ](?<time>\d{2}:\d{2}:\d{2})(?:\.(?<fraction>\d+))?\s*(?<offset>Z|[+-]\d{2}:?\d{2})?")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"Exited \((?<code>-?\d+)\)")]
    private static partial Regex ExitCodeRegex();
}
