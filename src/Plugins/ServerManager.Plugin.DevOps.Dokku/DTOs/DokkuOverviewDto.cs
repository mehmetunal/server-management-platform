namespace ServerManager.Plugin.DevOps.Dokku.DTOs;

public sealed class DokkuOverviewDto
{
    public bool IsInstalled { get; init; }

    public string? Version { get; init; }

    public string? HostError { get; init; }

    public IReadOnlyList<DokkuAppDto> Apps { get; init; } = [];

    public bool InstallRunning { get; init; }

    public bool? InstallSucceeded { get; init; }

    public string? InstallMessage { get; init; }

    public string? InstallLog { get; init; }
}
