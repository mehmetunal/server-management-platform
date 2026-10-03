namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerContainerDetailsDto
{
    public required DockerContainerDto Container { get; init; }

    public string? Command { get; init; }

    public string? Entrypoint { get; init; }

    public string? WorkingDir { get; init; }

    public string? User { get; init; }

    public string? Hostname { get; init; }

    public string? RestartPolicy { get; init; }

    public DateTime? FinishedAt { get; init; }

    public bool OomKilled { get; init; }

    public string? Error { get; init; }

    public string? Platform { get; init; }

    public IReadOnlyList<string> PortBindings { get; init; } = [];

    public IReadOnlyList<DockerMountDto> Mounts { get; init; } = [];

    public IReadOnlyList<DockerContainerNetworkDto> NetworkDetails { get; init; } = [];

    /// <summary>Yalnızca değişken adları; değerler gizli bilgi içerebileceği için hiçbir zaman taşınmaz.</summary>
    public IReadOnlyList<string> EnvironmentKeys { get; init; } = [];

    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    /// <summary>Ortam değişkeni değerleri maskelenmiş inspect çıktısı.</summary>
    public string InspectJson { get; init; } = string.Empty;
}
