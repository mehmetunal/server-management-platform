using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Web.Helpers;

namespace ServerManager.Web.Models;

public sealed class ResourcePayload
{
    public double Cpu { get; init; }

    public double Memory { get; init; }

    public double Disk { get; init; }

    public string CpuText { get; init; } = string.Empty;

    public string MemoryText { get; init; } = string.Empty;

    public string DiskText { get; init; } = string.Empty;

    public string LoadText { get; init; } = string.Empty;

    public string RxText { get; init; } = string.Empty;

    public string TxText { get; init; } = string.Empty;

    public string UptimeText { get; init; } = string.Empty;

    public string CollectedAtText { get; init; } = string.Empty;

    public static ResourcePayload From(ServerResourceSummaryDto summary) => new()
    {
        Cpu = summary.CpuUsagePercent,
        Memory = summary.MemoryUsagePercent,
        Disk = summary.DiskUsagePercent,
        CpuText = MetricDisplay.Percent(summary.CpuUsagePercent),
        MemoryText = MetricDisplay.Percent(summary.MemoryUsagePercent),
        DiskText = MetricDisplay.Percent(summary.DiskUsagePercent),
        LoadText = MetricDisplay.Number(summary.LoadAverage1),
        RxText = MetricDisplay.Rate(summary.NetworkRxBytesPerSecond),
        TxText = MetricDisplay.Rate(summary.NetworkTxBytesPerSecond),
        UptimeText = MetricDisplay.Uptime(summary.UptimeSeconds),
        CollectedAtText = DateDisplay.Format(summary.CollectedAt, "HH:mm:ss")
    };
}
