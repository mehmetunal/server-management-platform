using ServerManager.Plugin.DevOps.Dokploy.Domain;

namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

public sealed class DokployHealthResultDto
{
    public DokployStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;

    public DateTime CheckedAt { get; init; }

    public int? ResponseTimeMs { get; init; }

    public string? Version { get; init; }

    public bool IsHealthy => Status == DokployStatus.Running;
}
