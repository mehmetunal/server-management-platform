using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Commands;

public sealed record CommandRunListItemDto(
    Guid Id,
    string Command,
    string? TemplateName,
    CommandRunStatus Status,
    int TargetCount,
    int SucceededCount,
    int FailedCount,
    bool UseSudo,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? UserName);
