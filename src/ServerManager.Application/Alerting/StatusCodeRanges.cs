namespace ServerManager.Application.Alerting;

/// <summary>"200-399" veya "200,204,301-302" biçimindeki kabul edilen HTTP durum kodları.</summary>
public static class StatusCodeRanges
{
    public const string Default = "200-399";
    private const int MaxParts = 20;

    public static bool TryParse(string? value, out IReadOnlyList<(int From, int To)> ranges)
    {
        var parsed = new List<(int, int)>();
        ranges = parsed;
        var text = string.IsNullOrWhiteSpace(value) ? Default : value;
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 0 or > MaxParts)
            return false;

        foreach (var part in parts)
        {
            var bounds = part.Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length is 0 or > 2
                || !TryParseCode(bounds[0], out var from)
                || !TryParseCode(bounds[^1], out var to)
                || from > to)
            {
                return false;
            }

            parsed.Add((from, to));
        }

        return true;
    }

    public static bool IsAccepted(string? value, int statusCode) =>
        TryParse(value, out var ranges) && ranges.Any(r => statusCode >= r.From && statusCode <= r.To);

    private static bool TryParseCode(string text, out int code) =>
        int.TryParse(text, out code) && code is >= 100 and <= 599;
}
