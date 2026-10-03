using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Terminal;

public sealed record TerminalCommandEntry(
    Guid SessionId,
    Guid ServerId,
    string ServerName,
    TerminalActor Actor,
    DateTime ExecutedAt,
    string CommandText,
    bool IsApproximate,
    TerminalCommandStatus Status,
    string? MatchedRule);
