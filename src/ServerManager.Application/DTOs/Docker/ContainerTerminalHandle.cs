using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Application.DTOs.Docker;

public sealed class ContainerTerminalHandle
{
    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public string Container { get; init; } = string.Empty;

    public required ITerminalSession Session { get; init; }
}
