using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Web.Helpers;
using ServerManager.Web.Framework.UI;

namespace ServerManager.Web.Models;

public sealed class ResourcePayload
{
    public double Cpu { get; init; }

    public double Memory { get; init; }

    public double Disk { get; init; }

    public double Rx { get; init; }

    public double Tx { get; init; }

    public string CpuText { get; init; } = string.Empty;

    public string CpuDetailText { get; init; } = string.Empty;

    public string MemoryDetailText { get; init; } = string.Empty;

    public string DiskDetailText { get; init; } = string.Empty;

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
        Rx = summary.NetworkRxBytesPerSecond,
        Tx = summary.NetworkTxBytesPerSecond,
        CpuText = MetricDisplay.Percent(summary.CpuUsagePercent),
        MemoryText = MetricDisplay.Percent(summary.MemoryUsagePercent),
        DiskText = MetricDisplay.Percent(summary.DiskUsagePercent),
        CpuDetailText = MetricDisplay.CpuCores(summary.CpuUsagePercent, summary.CpuThreads),
        MemoryDetailText = MetricDisplay.UsedOfTotal(summary.MemoryUsedBytes, summary.MemoryTotalBytes),
        DiskDetailText = MetricDisplay.UsedOfTotal(summary.DiskUsedBytes, summary.DiskTotalBytes),
        LoadText = MetricDisplay.Number(summary.LoadAverage1),
        RxText = MetricDisplay.Rate(summary.NetworkRxBytesPerSecond),
        TxText = MetricDisplay.Rate(summary.NetworkTxBytesPerSecond),
        UptimeText = MetricDisplay.Uptime(summary.UptimeSeconds),
        CollectedAtText = DateDisplay.Format(summary.CollectedAt, "HH:mm:ss")
    };
}
