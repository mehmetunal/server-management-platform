namespace ServerManager.Plugin.DevOps.Dokku.DTOs;

public sealed class DokkuInstallProgress
{
    public bool IsRunning { get; init; }

    public bool? Succeeded { get; init; }

    public string? Message { get; init; }

    public string Log { get; init; } = string.Empty;
}
