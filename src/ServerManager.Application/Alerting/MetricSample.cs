namespace ServerManager.Application.Alerting;

public sealed record MetricSample(Guid ServerId, DateTime CollectedAt, double CpuPercent, double MemoryPercent, double DiskPercent);
