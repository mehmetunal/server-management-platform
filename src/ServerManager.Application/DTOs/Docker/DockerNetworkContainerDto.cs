namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerNetworkContainerDto
{
    public string Name { get; init; } = string.Empty;

    public string? IpAddress { get; init; }
}
