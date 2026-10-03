using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Monitoring;

namespace ServerManager.Application.Interfaces.Services;

public interface IMonitoringService
{
    Task<IReadOnlyList<Guid>> GetCollectableServerIdsAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<ServerMonitoringUpdateDto>> CollectAsync(Guid serverId, bool manual, CancellationToken cancellationToken = default);

    Task<ServiceResult<ServerMonitoringOverviewDto>> GetOverviewAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<MetricPointDto>>> GetSeriesAsync(Guid serverId, MetricRange range, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetricPointDto>> GetFleetSeriesAsync(MetricRange range, CancellationToken cancellationToken = default);

    Task<FleetResourceSummaryDto> GetFleetSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ServerResourceSummaryDto>> GetLatestSummariesAsync(IReadOnlyCollection<Guid> serverIds, CancellationToken cancellationToken = default);

    Task<MaintenanceResult> RunMaintenanceAsync(CancellationToken cancellationToken = default);
}
