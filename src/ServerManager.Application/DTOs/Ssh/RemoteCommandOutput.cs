namespace ServerManager.Application.DTOs.Ssh;

public sealed class RemoteCommandOutput
{
    public int? ExitCode { get; init; }

    public string Stdout { get; init; } = string.Empty;

    public string Stderr { get; init; } = string.Empty;

    public bool TimedOut { get; init; }

    public bool IsSuccess => !TimedOut && ExitCode == 0;
}
