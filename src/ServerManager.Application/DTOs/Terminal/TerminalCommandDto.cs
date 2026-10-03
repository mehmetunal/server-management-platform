using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Terminal;

public sealed class TerminalCommandDto
{
    public DateTime ExecutedAt { get; init; }

    public string CommandText { get; init; } = string.Empty;

    public bool IsApproximate { get; init; }

    public TerminalCommandStatus Status { get; init; }

    public string? MatchedRule { get; init; }
}
