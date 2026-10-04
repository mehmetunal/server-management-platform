using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Commands;

public sealed record CommandRunTargetDto(
    Guid Id,
    Guid ServerId,
    string ServerName,
    CommandTargetStatus Status,
    int? ExitCode,
    string? Output,
    bool OutputTruncated,
    string? ErrorMessage,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? DurationMs);
