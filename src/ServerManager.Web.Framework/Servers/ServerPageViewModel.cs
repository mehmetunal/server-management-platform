using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Monitoring;

namespace ServerManager.Web.Framework.Servers;

public sealed class ServerPageViewModel
{
    public const string OverviewTab = "overview";
    public const string MetricsTab = "metrics";
    public const string DockerTab = "docker";
    public const string TerminalTab = "terminal";
    public const string FilesTab = "files";
    public const string DeploymentsTab = "deployments";
    public const string SecurityTab = "security";
    public const string ServicesTab = "services";
    public const string ProcessesTab = "processes";
    public const string LogsTab = "logs";
    public const string NetworkTab = "network";
    public const string StorageTab = "storage";
    public const string ResourcesTab = "resources";
    public const string CleanupTab = "cleanup";
    public const string BackupsTab = "backups";
    public const string AlertsTab = "alerts";
    public const string ActivityTab = "activity";

    /// <summary>Servisler (tek tıkla Docker servisleri); sistem servisleri sekmesi <see cref="ServicesTab"/>'dır.</summary>
    public const string ManagedServicesTab = "managed-services";

    /// <summary>Çekirdek sekme sabitlerinden biri ya da bir eklentinin <see cref="ServerTab.Key"/> değeri.</summary>
    public string ActiveTab { get; init; } = OverviewTab;

    public required ServerDetailsDto Server { get; init; }

    public required ServerMonitoringOverviewDto Monitoring { get; init; }

    public required MonitoringOptions Thresholds { get; init; }

    public MetricRange Range { get; init; } = MetricRange.OneHour;
}
