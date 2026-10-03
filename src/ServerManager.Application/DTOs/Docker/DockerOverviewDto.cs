namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerOverviewDto
{
    public string? EngineVersion { get; init; }

    public string? OperatingSystem { get; init; }

    public string? KernelVersion { get; init; }

    public string? StorageDriver { get; init; }

    public string? DockerRootDir { get; init; }

    public int CpuCount { get; init; }

    public long MemoryTotalBytes { get; init; }

    public int ContainersTotal { get; init; }

    public int ContainersRunning { get; init; }

    public int ContainersStopped { get; init; }

    public int ContainersPaused { get; init; }

    public int ContainersRestarting { get; init; }

    public int ContainersFailed { get; init; }

    public int ContainersUnhealthy { get; init; }

    public int Images { get; init; }

    public int Volumes { get; init; }

    public int Networks { get; init; }

    public IReadOnlyList<DockerDiskUsageDto> DiskUsage { get; init; } = [];
}
