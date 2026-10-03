using ServerManager.Application.DTOs.Monitoring;

namespace ServerManager.Application.Interfaces.Monitoring;

public interface IMonitoringNotifier
{
    Task ServerUpdatedAsync(ServerMonitoringUpdateDto update, CancellationToken cancellationToken = default);
}
