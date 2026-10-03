namespace ServerManager.Application.DTOs.Ssh;

public sealed class RemoteExecutionContext
{
    public required SshConnectionRequest Connection { get; init; }

    public bool UseSudo { get; init; }

    public string? SudoPassword { get; init; }

    public override string ToString() => Connection.ToString();
}
