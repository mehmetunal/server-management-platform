namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

public sealed class DokployOverviewDto
{
    public DokployInstanceDto? Instance { get; init; }

    public DokployHostStatusDto? Host { get; init; }

    /// <summary>SSH ile durum okunamadıysa nedeni.</summary>
    public string? HostError { get; init; }

    public IReadOnlyList<DokployInstallationDto> Installations { get; init; } = [];

    public Guid? RunningInstallationId { get; init; }
}
