namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerContainerNetworkDto
{
    public string Name { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? Gateway { get; init; }

    public string? MacAddress { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = [];
}
