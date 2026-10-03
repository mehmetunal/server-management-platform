using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Monitoring;

public interface IMetricsCollector
{
    Task<MetricsCollectionResult> CollectAsync(SshConnectionRequest request, CancellationToken cancellationToken = default);
}
