namespace ServerManager.Application.Common;

public static class TagParser
{
    public const int MaxTagCount = 20;
    public const int MaxTagLength = 64;

    private static readonly char[] Separators = [',', ';', '\n', '\r'];

    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static string Join(IEnumerable<string> tags) => string.Join(", ", tags);
}
