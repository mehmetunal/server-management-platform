namespace ServerManager.Application.DTOs.Ssh;

public sealed class ServerConnection
{
    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public required RemoteExecutionContext Context { get; init; }
}
