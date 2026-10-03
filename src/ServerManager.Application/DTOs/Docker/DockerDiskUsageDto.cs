namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerDiskUsageDto
{
    public string Type { get; init; } = string.Empty;

    public int TotalCount { get; init; }

    public int ActiveCount { get; init; }

    public long SizeBytes { get; init; }

    public long ReclaimableBytes { get; init; }
}
