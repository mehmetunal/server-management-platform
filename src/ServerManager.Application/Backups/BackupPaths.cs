namespace ServerManager.Application.Backups;

public static class BackupPaths
{
    public const int MaxPathLength = 1024;
    public const int MaxPathCount = 50;
    public const int MaxExcludeLength = 255;
    public const int MaxExcludeCount = 50;

    private static readonly string[] PseudoFileSystems = ["/proc", "/sys", "/dev"];

    /// <summary>Yolu normalleştirir (çift / ve sondaki / kaldırılır). Mutlak değilse, ".." içeriyorsa veya kontrol karakteri varsa null.</summary>
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        path = path.Trim();
        if (path.Length > MaxPathLength || path[0] != '/' || path.Any(char.IsControl))
            return null;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s is ".." or "."))
            return null;

        return "/" + string.Join('/', segments);
    }

    public static bool IsPseudoFileSystem(string normalizedPath) =>
        PseudoFileSystems.Any(root => normalizedPath == root || normalizedPath.StartsWith(root + "/", StringComparison.Ordinal));

    /// <summary>Satırlara böler; boş satırlar atlanır, tekrarlar kaldırılır.</summary>
    public static IReadOnlyList<string> SplitLines(string? value) =>
        (value ?? string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>tar arşivindeki göreli ad: /etc/nginx -> etc/nginx.</summary>
    public static string ToArchiveName(string normalizedPath) => normalizedPath.TrimStart('/');
}
