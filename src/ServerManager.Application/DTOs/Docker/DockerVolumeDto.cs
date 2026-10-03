namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerVolumeDto
{
    public string Name { get; init; } = string.Empty;

    public string Driver { get; init; } = string.Empty;

    public string Mountpoint { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public long? SizeBytes { get; init; }

    public IReadOnlyList<string> Containers { get; init; } = [];

    public string? ComposeProject { get; init; }
}
