using System.Text.Json.Serialization;

namespace ServerManager.Application.ResourceUsage;

/// <summary>Kaynak geçmişi toplayıcısının bir container için okuduğu durum (docker inspect) ve kullanım (docker stats).</summary>
public sealed record ContainerStateFact(
    string Name,
    string State,
    string? Health,
    int RestartCount,
    double CpuPercent,
    long MemoryUsageBytes,
    long MemoryLimitBytes,
    double MemoryPercent,
    long NetworkRxBytes,
    long NetworkTxBytes,
    long BlockReadBytes,
    long BlockWriteBytes)
{
    public bool IsRunning => string.Equals(State, "running", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Process'in örnek anındaki kullanımı. JSON alan adları kısa tutulur (anlık görüntü satırında saklanır).</summary>
/// <param name="CpuPercent">İki /proc okuması arasındaki gerçek CPU kullanımı (çekirdek başına %, top gibi 100'ü aşabilir).</param>
public sealed record ProcessSample(
    [property: JsonPropertyName("p")] int Pid,
    [property: JsonPropertyName("u")] string User,
    [property: JsonPropertyName("c")] double CpuPercent,
    [property: JsonPropertyName("m")] double MemoryPercent,
    [property: JsonPropertyName("r")] long ResidentKilobytes,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("a")] string Command);

/// <summary>Bir toplama turunun ham verisi.</summary>
public sealed class ResourceHistoryFacts
{
    /// <summary>Sunucunun toplam CPU kullanımı (%), /proc/stat'tan; okunamadıysa null.</summary>
    public double? CpuBusyPercent { get; init; }

    public IReadOnlyList<ProcessSample> Processes { get; init; } = [];

    public bool DockerAvailable { get; init; }

    public IReadOnlyList<ContainerStateFact> Containers { get; init; } = [];
}

/// <summary>Grafik serisi (container başına bir çizgi).</summary>
public sealed record HistorySeries(string Name, IReadOnlyList<double?> Data);

/// <summary>Aralıkta container başına özet.</summary>
public sealed record ContainerUsageSummary(
    string Name,
    int Samples,
    double CpuAvg,
    double CpuMax,
    double MemoryAvgBytes,
    long MemoryMaxBytes,
    long? NetworkBytes,
    long? BlockBytes,
    int Restarts);

/// <summary>Aralıktaki anlık görüntülerden process (komut adı) başına özet.</summary>
/// <param name="Seen">Process'in listede göründüğü anlık görüntü sayısı.</param>
public sealed record ProcessUsageSummary(string Name, string? User, double CpuAvg, double CpuMax, long ResidentMaxKilobytes, int Seen);

public sealed class ResourceHistoryReport
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    /// <summary>raw: ham örnekler, hourly: saatlik özetler.</summary>
    public string Source { get; init; } = "raw";

    public IReadOnlyList<DateTime> Timestamps { get; init; } = [];

    public IReadOnlyList<HistorySeries> Cpu { get; init; } = [];

    public IReadOnlyList<HistorySeries> Memory { get; init; } = [];

    public IReadOnlyList<ContainerUsageSummary> Containers { get; init; } = [];

    public IReadOnlyList<ProcessUsageSummary> ProcessesByCpu { get; init; } = [];

    public IReadOnlyList<ProcessUsageSummary> ProcessesByMemory { get; init; } = [];

    public int SnapshotCount { get; init; }
}

public sealed class ProcessSnapshotView
{
    public DateTime CollectedAt { get; init; }

    public double? CpuBusyPercent { get; init; }

    public IReadOnlyList<ProcessSample> Processes { get; init; } = [];
}

/// <summary>Grafik noktası satırı (depodan).</summary>
public sealed record ContainerSeriesPoint(DateTime Bucket, string ContainerName, double CpuPercent, double MemoryBytes);

/// <summary>Depodan okunan process anlık görüntüsü (JSON çözülmemiş).</summary>
public sealed record ProcessSnapshotRow(DateTime CollectedAt, double? CpuBusyPercent, string ProcessesJson);
