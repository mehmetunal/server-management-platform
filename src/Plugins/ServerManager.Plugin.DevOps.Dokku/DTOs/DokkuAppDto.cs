namespace ServerManager.Plugin.DevOps.Dokku.DTOs;

public sealed class DokkuAppDto
{
    public string Name { get; init; } = string.Empty;

    public bool? Deployed { get; init; }

    public bool? Running { get; init; }

    public string? Domains { get; init; }
}
