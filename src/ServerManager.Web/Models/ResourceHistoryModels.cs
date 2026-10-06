using ServerManager.Application.Common;
using ServerManager.Application.ResourceUsage;

namespace ServerManager.Web.Models;

/// <summary>"Kaynak Kullanımı → Geçmiş" için JSON: grafik serileri ve aralıkta en çok kaynak kullananlar.</summary>
public sealed class ResourceHistoryModel
{
    public string From { get; init; } = string.Empty;

    public string To { get; init; } = string.Empty;

    public string Source { get; init; } = "raw";

    /// <summary>Gösterim için yerel saat etiketleri.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>Noktaya tıklanınca en yakın process anlık görüntüsünü istemek için UTC zamanlar (ISO 8601).</summary>
    public IReadOnlyList<string> Timestamps { get; init; } = [];

    public IReadOnlyList<HistorySeries> Cpu { get; init; } = [];

    public IReadOnlyList<HistorySeries> Memory { get; init; } = [];

    public IReadOnlyList<ContainerUsageSummary> Containers { get; init; } = [];

    public IReadOnlyList<ProcessUsageSummary> ProcessesByCpu { get; init; } = [];

    public IReadOnlyList<ProcessUsageSummary> ProcessesByMemory { get; init; } = [];

    public int SnapshotCount { get; init; }

    public string Description { get; init; } = string.Empty;

    public static ResourceHistoryModel Create(ResourceHistoryReport report)
    {
        var span = report.To - report.From;
        var labelFormat = span > TimeSpan.FromHours(20) ? "dd.MM HH:mm" : "HH:mm";
        var from = AppTimeZone.ToLocal(report.From);
        var to = AppTimeZone.ToLocal(report.To);
        return new ResourceHistoryModel
        {
            From = from.ToString("yyyy-MM-ddTHH:mm"),
            To = to.ToString("yyyy-MM-ddTHH:mm"),
            Source = report.Source,
            Labels = report.Timestamps.Select(t => AppTimeZone.ToLocal(t).ToString(labelFormat)).ToList(),
            Timestamps = report.Timestamps.Select(Iso).ToList(),
            Cpu = report.Cpu,
            Memory = report.Memory,
            Containers = report.Containers,
            ProcessesByCpu = report.ProcessesByCpu,
            ProcessesByMemory = report.ProcessesByMemory,
            SnapshotCount = report.SnapshotCount,
            Description = $"{from:dd.MM.yyyy HH:mm} – {to:dd.MM.yyyy HH:mm} · " +
                          (report.Source == "hourly" ? "saatlik ortalamalar" : "ham örnekler") +
                          $" · {report.SnapshotCount} process anlık görüntüsü"
        };
    }

    public static string Iso(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
}

public sealed class ProcessSnapshotModel
{
    public string CollectedAt { get; init; } = string.Empty;

    public double? CpuBusyPercent { get; init; }

    public IReadOnlyList<ProcessSample> Processes { get; init; } = [];

    public static ProcessSnapshotModel Create(ProcessSnapshotView view) => new()
    {
        CollectedAt = AppTimeZone.ToLocal(view.CollectedAt).ToString("dd.MM.yyyy HH:mm:ss"),
        CpuBusyPercent = view.CpuBusyPercent,
        Processes = view.Processes
    };
}
