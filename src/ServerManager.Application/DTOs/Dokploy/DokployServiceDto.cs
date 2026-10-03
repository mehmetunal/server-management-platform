namespace ServerManager.Application.DTOs.Dokploy;

/// <summary>Dokploy'a ait Docker Swarm servisi.</summary>
public sealed class DokployServiceDto
{
    public string Name { get; init; } = string.Empty;

    public string Image { get; init; } = string.Empty;

    public int RunningReplicas { get; init; }

    public int DesiredReplicas { get; init; }

    public string? Ports { get; init; }

    public bool IsRunning => DesiredReplicas > 0 && RunningReplicas >= DesiredReplicas;
}
