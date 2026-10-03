namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerContainerStatsDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public double CpuPercent { get; init; }

    public long MemoryUsageBytes { get; init; }

    public long MemoryLimitBytes { get; init; }

    public double MemoryPercent { get; init; }

    public long NetworkRxBytes { get; init; }

    public long NetworkTxBytes { get; init; }

    public long BlockReadBytes { get; init; }

    public long BlockWriteBytes { get; init; }

    public int Pids { get; init; }
}
