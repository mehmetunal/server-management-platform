using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IServerMetricRepository
{
    Task AddMetricAsync(ServerMetric metric, CancellationToken cancellationToken = default);

    Task AddHealthCheckAsync(ServerHealthCheck healthCheck, CancellationToken cancellationToken = default);

    Task UpsertSnapshotAsync(Guid serverId, SystemMetricsSnapshot snapshot, DateTime collectedAt, CancellationToken cancellationToken = default);

    Task<(SystemMetricsSnapshot Snapshot, DateTime CollectedAt)?> GetSnapshotAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServerMetric?> GetLatestAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ServerMetric>> GetLatestForServersAsync(IReadOnlyCollection<Guid> serverIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetricPointDto>> GetRawSeriesAsync(Guid? serverId, DateTime fromUtc, int bucketSeconds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetricPointDto>> GetHourlySeriesAsync(Guid? serverId, DateTime fromUtc, int bucketSeconds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServerHealthCheck>> GetRecentHealthChecksAsync(Guid serverId, int count, CancellationToken cancellationToken = default);

    Task<double?> GetUptimePercentAsync(Guid serverId, DateTime sinceUtc, CancellationToken cancellationToken = default);

    Task<FleetResourceSummaryDto> GetFleetSummaryAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);

    Task<int> AggregateHourlyAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredAsync(RetentionTarget target, DateTime cutoffUtc, int keepLatestPerServer, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
