using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Monitoring;

namespace ServerManager.Web.Models;

public sealed class MetricsPanelViewModel
{
    public required ServerMonitoringOverviewDto Monitoring { get; init; }

    public required MonitoringOptions Thresholds { get; init; }
}
