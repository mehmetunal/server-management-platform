using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Web.Hubs;
using ServerManager.Web.Models;

namespace ServerManager.Web.Services;

public sealed class SignalRMonitoringNotifier : IMonitoringNotifier
{
    private readonly IHubContext<MonitoringHub> _hubContext;

    public SignalRMonitoringNotifier(IHubContext<MonitoringHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task ServerUpdatedAsync(ServerMonitoringUpdateDto update, CancellationToken cancellationToken = default)
    {
        var payload = MonitoringUpdatePayload.From(update);
        return _hubContext.Clients
            .Groups(MonitoringHub.ServerGroup(update.ServerId), MonitoringHub.FleetGroup)
            .SendAsync(MonitoringHub.ServerUpdatedEvent, payload, cancellationToken);
    }
}
