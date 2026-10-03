using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Terminal;

public sealed class TerminalHandle
{
    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public TerminalSessionKind Kind { get; init; }

    public string? Container { get; init; }

    public required ITerminalSession Session { get; init; }
}
