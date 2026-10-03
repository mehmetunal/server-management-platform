using Microsoft.Extensions.Options;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;

namespace ServerManager.Web.Framework.Servers;

public sealed class ServerPageBuilder
{
    private readonly IServerService _serverService;
    private readonly IMonitoringService _monitoringService;
    private readonly MonitoringOptions _monitoringOptions;

    public ServerPageBuilder(IServerService serverService, IMonitoringService monitoringService, IOptions<MonitoringOptions> monitoringOptions)
    {
        _serverService = serverService;
        _monitoringService = monitoringService;
        _monitoringOptions = monitoringOptions.Value;
    }

    public async Task<ServerPageViewModel?> BuildAsync(Guid id, string activeTab, MetricRange range, CancellationToken cancellationToken)
    {
        var details = await _serverService.GetDetailsAsync(id, cancellationToken);
        if (!details.IsSuccess)
            return null;

        var monitoring = await _monitoringService.GetOverviewAsync(id, cancellationToken);
        if (!monitoring.IsSuccess)
            return null;

        return new ServerPageViewModel
        {
            Server = details.Data!,
            Monitoring = monitoring.Data!,
            Thresholds = _monitoringOptions,
            ActiveTab = activeTab,
            Range = range
        };
    }
}
