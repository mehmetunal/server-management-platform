using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Monitoring;

namespace ServerManager.Web.Models;

public sealed class ServerPageViewModel
{
    public const string OverviewTab = "overview";
    public const string MetricsTab = "metrics";
    public const string DockerTab = "docker";
    public const string TerminalTab = "terminal";
    public const string FilesTab = "files";
    public const string DokployTab = "dokploy";

    public required ServerDetailsDto Server { get; init; }

    public required ServerMonitoringOverviewDto Monitoring { get; init; }

    public required MonitoringOptions Thresholds { get; init; }

    public string ActiveTab { get; init; } = OverviewTab;

    public MetricRange Range { get; init; } = MetricRange.OneHour;
}
