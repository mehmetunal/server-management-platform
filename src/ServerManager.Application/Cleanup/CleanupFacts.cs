using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.Cleanup;

/// <summary>
/// Sunucudan okunan ham bilgiler; hangi öğenin listeleneceği ve güvenlik seviyesi <see cref="CleanupClassifier"/>'da belirlenir.
/// </summary>
public sealed class CleanupFacts
{
    public bool DockerAvailable { get; init; }

    public string? DockerMessage { get; init; }

    public IReadOnlyList<DockerContainerFact> Containers { get; init; } = [];

    public IReadOnlyList<DockerImageFact> Images { get; init; } = [];

    public IReadOnlyList<DockerVolumeFact> Volumes { get; init; } = [];

    /// <summary>Hiçbir container'ın bağlı olmadığı ağlar (<c>docker network ls --filter dangling=true</c>).</summary>
    public IReadOnlyList<DockerNetworkFact> UnusedNetworks { get; init; } = [];

    /// <summary>Kullanımda olmayan build cache kayıtlarının toplam boyutu.</summary>
    public long BuildCacheReclaimableBytes { get; init; }

    public int BuildCacheEntries { get; init; }

    /// <summary>apt, dnf veya yum; bulunamazsa null.</summary>
    public string? PackageManager { get; init; }

    public long? PackageCacheBytes { get; init; }

    public bool JournalAvailable { get; init; }

    public long? JournalBytes { get; init; }

    public FileSetFact RotatedLogs { get; init; } = FileSetFact.Empty;

    public FileSetFact TempFiles { get; init; } = FileSetFact.Empty;

    public bool SnapAvailable { get; init; }

    public IReadOnlyList<SnapRevisionFact> DisabledSnaps { get; init; } = [];

    public string? RunningKernel { get; init; }

    public IReadOnlyList<KernelFact> Kernels { get; init; } = [];
}

public sealed record DockerContainerFact(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    long? SizeBytes,
    IReadOnlyDictionary<string, string> Labels)
{
    public string? ComposeProject => Labels.TryGetValue(PanelOwnership.ComposeProjectLabel, out var value) && value.Length > 0 ? value : null;
}

public sealed record DockerImageFact(
    string Id,
    string Repository,
    string Tag,
    long SizeBytes,
    int Containers,
    DateTime? CreatedAt)
{
    public bool IsDangling => Repository is "<none>" or "" && Tag is "<none>" or "";

    public string Reference => IsDangling || Tag is "<none>" or "" ? Id : $"{Repository}:{Tag}";
}

/// <param name="Links">Volume'u kullanan container sayısı; okunamazsa null.</param>
public sealed record DockerVolumeFact(
    string Name,
    int? Links,
    long? SizeBytes,
    IReadOnlyDictionary<string, string> Labels);

public sealed record DockerNetworkFact(
    string Id,
    string Name,
    string Driver,
    IReadOnlyDictionary<string, string> Labels);

/// <summary>find ile bulunan dosya kümesi: toplam ve en büyük birkaç dosya (önizleme için).</summary>
public sealed record FileSetFact(int Count, long TotalBytes, IReadOnlyList<FileFact> Largest)
{
    public static readonly FileSetFact Empty = new(0, 0, []);
}

public sealed record FileFact(string Path, long SizeBytes, DateTime? ModifiedAt);

public sealed record SnapRevisionFact(string Name, string Revision, long? SizeBytes);

public sealed record KernelFact(string Package, string Version, long? SizeBytes);
