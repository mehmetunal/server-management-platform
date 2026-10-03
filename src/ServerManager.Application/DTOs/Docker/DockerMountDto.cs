namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerMountDto
{
    public string Type { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string Source { get; init; } = string.Empty;

    public string Destination { get; init; } = string.Empty;

    public string? Mode { get; init; }

    public bool ReadWrite { get; init; }
}
