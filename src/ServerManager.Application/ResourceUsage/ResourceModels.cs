using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.ResourceUsage;

/// <summary>/proc/meminfo özeti (kB).</summary>
public sealed record MemoryInfo(
    long TotalKilobytes,
    long AvailableKilobytes,
    long FreeKilobytes,
    long BuffersKilobytes,
    long CachedKilobytes,
    long SharedKilobytes,
    long SwapTotalKilobytes,
    long SwapFreeKilobytes)
{
    public long UsedKilobytes => Math.Max(0, TotalKilobytes - AvailableKilobytes);

    /// <summary>Çekirdeğin gerektiğinde geri verebileceği önbellek (page cache + buffers).</summary>
    public long CacheKilobytes => BuffersKilobytes + CachedKilobytes;

    public long SwapUsedKilobytes => Math.Max(0, SwapTotalKilobytes - SwapFreeKilobytes);

    public double UsedPercent => TotalKilobytes > 0 ? UsedKilobytes * 100d / TotalKilobytes : 0;

    public double AvailablePercent => TotalKilobytes > 0 ? AvailableKilobytes * 100d / TotalKilobytes : 0;

    public double? SwapUsedPercent => SwapTotalKilobytes > 0 ? SwapUsedKilobytes * 100d / SwapTotalKilobytes : null;
}

/// <summary>1 saniyelik örnekte CPU zamanının dağılımı (%).</summary>
/// <param name="Source">vmstat veya /proc/stat.</param>
public sealed record CpuBreakdown(double User, double System, double Idle, double IoWait, double Steal, string Source)
{
    public double Busy => Math.Max(0, 100 - Idle - IoWait);
}

public sealed record SwapProcess(int Pid, string Name, long SwapKilobytes);

public sealed record IoProcess(int Pid, string? User, string Command, double ReadKilobytesPerSecond, double WriteKilobytesPerSecond)
{
    public double TotalKilobytesPerSecond => ReadKilobytesPerSecond + WriteKilobytesPerSecond;
}

/// <summary>Sunucunun anlık kaynak durumu (tek SSH betiğiyle okunur).</summary>
public sealed class ResourceSnapshot
{
    public int? CpuCores { get; init; }

    public double? Load1 { get; init; }

    public double? Load5 { get; init; }

    public double? Load15 { get; init; }

    public long? UptimeSeconds { get; init; }

    public MemoryInfo? Memory { get; init; }

    public CpuBreakdown? Cpu { get; init; }

    public IReadOnlyList<SwapProcess> SwapProcesses { get; init; } = [];

    /// <summary>Son 24 saatteki OOM (bellek yetersizliği) satırları; journal yoksa dmesg'den (zaman sınırı olmadan).</summary>
    public IReadOnlyList<string> OomEvents { get; init; } = [];

    public string? OomSource { get; init; }

    /// <summary>pidstat varsa en çok disk okuyan / yazan process'ler.</summary>
    public bool IoToolAvailable { get; init; }

    public IReadOnlyList<IoProcess> IoProcesses { get; init; } = [];
}

/// <summary>docker stats satırı ve container etiketleri (panel projesi / servis eşlemesi için).</summary>
public sealed record ContainerUsageFact(DockerContainerStatsDto Stats, IReadOnlyDictionary<string, string> Labels);

public sealed record ContainerUsage(DockerContainerStatsDto Stats, PanelOwner? Owner, string? ComposeProject)
{
    public string Name => Stats.Name;
}

/// <summary>Inspector'ın döndürdüğü ham veriler.</summary>
public sealed class ResourceFacts
{
    public required ResourceSnapshot Snapshot { get; init; }

    public ProcessList? Processes { get; init; }

    public StorageSnapshot? Storage { get; init; }

    /// <summary>Docker istenmediyse veya yoksa null.</summary>
    public IReadOnlyList<ContainerUsageFact>? Containers { get; init; }

    public string? DockerMessage { get; init; }
}

public sealed class ResourceOverview
{
    public required ResourceSnapshot Snapshot { get; init; }

    public ProcessList? Processes { get; init; }

    public StorageSnapshot? Storage { get; init; }

    public bool DockerIncluded { get; init; }

    public IReadOnlyList<ContainerUsage>? Containers { get; init; }

    public string? DockerMessage { get; init; }

    public IReadOnlyList<ResourceFinding> Findings { get; init; } = [];

    public DateTime CollectedAt { get; init; }

    public IReadOnlyList<ProcessEntry> TopCpu(int count) =>
        Processes is null ? [] : Processes.Processes.OrderByDescending(p => p.CpuPercent ?? 0).Take(count).ToList();

    public IReadOnlyList<ProcessEntry> TopMemory(int count) =>
        Processes is null ? [] : Processes.Processes.OrderByDescending(p => p.ResidentKilobytes ?? 0).Take(count).ToList();
}

/// <summary>du / find ile bulunan en büyük klasörler ve dosyalar.</summary>
public sealed record DiskUsageReport(
    string Path,
    IReadOnlyList<DirectoryUsage> Directories,
    IReadOnlyList<LargeFile> Files,
    bool DirectoriesTimedOut,
    bool FilesTimedOut,
    int TimeoutSeconds);

public sealed record DirectoryUsage(string Path, long SizeKilobytes, int Depth);

public sealed record LargeFile(string Path, long SizeBytes, DateTime? ModifiedAt, string? Owner);
