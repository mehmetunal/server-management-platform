namespace ServerManager.Application.ResourceUsage;

public static class ResourceRules
{
    public const string DefaultScanPath = "/";
    public const int MaxPathLength = 256;

    /// <summary>du ve find'ın her biri için süre sınırı (timeout varsa).</summary>
    public const int ScanTimeoutSeconds = 60;

    /// <summary>du --threshold: bundan küçük klasörler listelenmez (GNU du).</summary>
    public const int DirectoryThresholdMegabytes = 10;

    /// <summary>find -size: bundan büyük dosyalar listelenir.</summary>
    public const int LargeFileMegabytes = 100;

    public const int MaxDirectories = 40;
    public const int MaxFiles = 20;
    public const int TopProcessCount = 10;

    private static readonly string[] ForbiddenRoots = ["/proc", "/sys", "/dev"];

    /// <summary>Mutlak, normalize edilmiş bir yol; '..', kontrol karakterleri ve sanal dosya sistemleri reddedilir.</summary>
    public static bool IsValidScanPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > MaxPathLength || path[0] != '/')
            return false;
        if (path.Any(char.IsControl))
            return false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s is "." or ".."))
            return false;

        var normalized = NormalizePath(path);
        return !ForbiddenRoots.Any(root => normalized == root || normalized.StartsWith(root + "/", StringComparison.Ordinal));
    }

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return DefaultScanPath;

        var segments = path.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries);
        return "/" + string.Join('/', segments);
    }
}
