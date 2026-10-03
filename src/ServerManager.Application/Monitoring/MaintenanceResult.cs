namespace ServerManager.Application.Monitoring;

public sealed record MaintenanceResult(int AggregatedHours, int DeletedRawMetrics, int DeletedHourlyMetrics, int DeletedHealthChecks);
