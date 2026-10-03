namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

/// <summary>Dokploy projesinin özeti. API yanıtındaki parola ve ortam değişkenleri bilinçli olarak alınmaz.</summary>
public sealed class DokployProjectDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateTime? CreatedAt { get; init; }

    public int EnvironmentCount { get; init; }

    public int ApplicationCount { get; init; }

    public int ComposeCount { get; init; }

    public int DatabaseCount { get; init; }
}
