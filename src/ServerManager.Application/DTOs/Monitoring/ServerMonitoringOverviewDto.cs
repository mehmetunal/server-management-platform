namespace ServerManager.Application.DTOs.Monitoring;

public sealed class ServerMonitoringOverviewDto
{
    public Guid ServerId { get; init; }

    public bool MonitoringEnabled { get; init; }

    public bool HasTrustedHostKey { get; init; }

    public DateTime? LastSeenAt { get; init; }

    public DateTime? SnapshotCollectedAt { get; init; }

    public SystemMetricsSnapshot? Snapshot { get; init; }

    public ServerResourceSummaryDto? Latest { get; init; }

    public double? UptimePercent24h { get; init; }

    public IReadOnlyList<HealthCheckDto> RecentHealthChecks { get; init; } = [];
}
