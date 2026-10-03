using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Terminal;

public sealed class TerminalSessionDto
{
    public Guid Id { get; init; }

    public Guid ServerId { get; init; }

    public string? UserName { get; init; }

    public string? IpAddress { get; init; }

    public TerminalSessionKind Kind { get; init; }

    public string? Container { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? EndedAt { get; init; }

    public string? CloseReason { get; init; }

    public int CommandCount { get; init; }
}
