using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

/// <summary>Bir sunucunun belirli bir andan bu yana ham metrik özeti; uzun süreli kurallarda satırlar belleğe alınmaz.</summary>
public sealed record MetricWindowStats(
    Guid ServerId,
    int SampleCount,
    DateTime FirstCollectedAt,
    double MinCpuPercent,
    double MinMemoryPercent,
    double MinDiskPercent,
    double AvgCpuPercent,
    double AvgMemoryPercent,
    double AvgDiskPercent)
{
    public double Minimum(AlertRuleKind kind) => kind switch
    {
        AlertRuleKind.CpuUsage => MinCpuPercent,
        AlertRuleKind.MemoryUsage => MinMemoryPercent,
        AlertRuleKind.DiskUsage => MinDiskPercent,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Metrik kuralı değil.")
    };

    public double Average(AlertRuleKind kind) => kind switch
    {
        AlertRuleKind.CpuUsage => AvgCpuPercent,
        AlertRuleKind.MemoryUsage => AvgMemoryPercent,
        AlertRuleKind.DiskUsage => AvgDiskPercent,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Metrik kuralı değil.")
    };
}
