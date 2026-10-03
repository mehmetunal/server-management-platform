using ServerManager.Plugin.DevOps.Dokploy.Domain;

namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

public sealed class DokployInstanceDto
{
    public string BaseUrl { get; init; } = string.Empty;

    public int? Port { get; init; }

    public bool HasApiKey { get; init; }

    public string? Version { get; init; }

    public DokployStatus Status { get; init; }

    public string? StatusMessage { get; init; }

    public DateTime? LastHealthCheckAt { get; init; }

    public int? LastResponseTimeMs { get; init; }

    public DateTime? InstalledAt { get; init; }

    public bool InstalledByPanel { get; init; }
}
