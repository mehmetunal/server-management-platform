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

    public IReadOnlyList<string> EffectiveProtectedPaths => ProtectedPaths.Count > 0 ? ProtectedPaths : DefaultProtectedPaths;
}
