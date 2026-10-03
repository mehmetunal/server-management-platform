namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

/// <summary>Sunucu üzerinde (SSH ile) görülen Dokploy durumu.</summary>
public sealed class DokployHostStatusDto
{
    public bool DockerAvailable { get; init; }

    public string? DockerError { get; init; }

    public bool SwarmActive { get; init; }

    public IReadOnlyList<DokployServiceDto> Services { get; init; } = [];

    public IReadOnlyList<DokployContainerDto> Containers { get; init; } = [];

    /// <summary>dokploy servisinin image etiketi (ör. v0.25.3, latest).</summary>
    public string? ImageTag { get; init; }

    /// <summary>Sunucu içinden 127.0.0.1 üzerindeki /api/health yanıtı.</summary>
    public bool LocalHealthy { get; init; }

    public bool IsInstalled => Services.Any(s => s.Name == "dokploy");
}
