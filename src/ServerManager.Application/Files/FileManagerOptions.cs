namespace ServerManager.Application.Files;

public sealed class FileManagerOptions
{
    public const string SectionName = "Files";

    /// <summary>Web editöründe açılabilecek en büyük dosya.</summary>
    public int MaxEditKilobytes { get; set; } = 1024;

    public int MaxUploadMegabytes { get; set; } = 100;

    public int OperationTimeoutSeconds { get; set; } = 60;

    /// <summary>Silme, taşıma ve izin değişikliğinin yapılamayacağı yollar. Boş bırakılırsa <see cref="DefaultProtectedPaths"/> kullanılır.</summary>
    public List<string> ProtectedPaths { get; set; } = [];

    public static IReadOnlyList<string> DefaultProtectedPaths { get; } =
    [
        "/", "/bin", "/boot", "/dev", "/etc", "/home", "/lib", "/lib64", "/opt", "/proc",
        "/root", "/run", "/sbin", "/srv", "/sys", "/tmp", "/usr", "/var"
    ];

    /// <summary>
    /// İşletim sistemine ait klasörler. Alt öğeleri etkileyen işlemler (klasör silme, alt öğelerle izin değişikliği) bunların
    /// kendisinde ve altındaki her yolda (ör. /usr/bin, /etc/ssh) yapılamaz.
    /// </summary>
    public static IReadOnlyList<string> SystemDirectories { get; } =
    [
        "/bin", "/boot", "/dev", "/etc", "/lib", "/lib32", "/lib64", "/proc", "/run", "/sbin", "/sys", "/usr"
    ];

    public IReadOnlyList<string> EffectiveProtectedPaths => ProtectedPaths.Count > 0 ? ProtectedPaths : DefaultProtectedPaths;
}
