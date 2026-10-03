using ServerManager.Application.DTOs.Dashboard;
using ServerManager.Web.Helpers;

namespace ServerManager.Web.Models;

public sealed class DashboardLiveModel
{
    public int TotalServers { get; init; }

    public int OnlineServers { get; init; }

    public int OfflineServers { get; init; }

    public int CriticalServers { get; init; }

    public int ReportingServers { get; init; }

    public string ReportingText { get; init; } = string.Empty;

    public string CpuText { get; init; } = string.Empty;

    public string MemoryText { get; init; } = string.Empty;

    public string DiskText { get; init; } = string.Empty;

    public string RxText { get; init; } = string.Empty;

    public string TxText { get; init; } = string.Empty;

    public static DashboardLiveModel From(DashboardSummaryDto summary) => new()
    {
        TotalServers = summary.TotalServers,
        OnlineServers = summary.OnlineServers,
        OfflineServers = summary.OfflineServers,
        CriticalServers = summary.CriticalServers,
        ReportingServers = summary.Resources.ReportingServers,
        ReportingText = summary.Resources.ReportingServers > 0
            ? $"{summary.Resources.ReportingServers} sunucunun ortalaması"
            : "Henüz metrik gelmedi",
        CpuText = MetricDisplay.Percent(summary.Resources.AverageCpuUsagePercent),
        MemoryText = MetricDisplay.Percent(summary.Resources.AverageMemoryUsagePercent),
        DiskText = MetricDisplay.Percent(summary.Resources.AverageDiskUsagePercent),
        RxText = MetricDisplay.Rate(summary.Resources.ReportingServers > 0 ? summary.Resources.TotalNetworkRxBytesPerSecond : null),
        TxText = MetricDisplay.Rate(summary.Resources.ReportingServers > 0 ? summary.Resources.TotalNetworkTxBytesPerSecond : null)
    };
}
