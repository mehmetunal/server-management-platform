namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerNetworkDto
{
    public string Id { get; init; } = string.Empty;

    public string ShortId => Id.Length > 12 ? Id[..12] : Id;

    public string Name { get; init; } = string.Empty;

    public string Driver { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public bool Internal { get; init; }

    public IReadOnlyList<string> Subnets { get; init; } = [];

    public IReadOnlyList<string> Gateways { get; init; } = [];

    public IReadOnlyList<DockerNetworkContainerDto> Containers { get; init; } = [];

    public DateTime? CreatedAt { get; init; }

    /// <summary>bridge, host ve none Docker tarafından yönetilir; silinemez.</summary>
    public bool IsSystem => Name is "bridge" or "host" or "none";
}
