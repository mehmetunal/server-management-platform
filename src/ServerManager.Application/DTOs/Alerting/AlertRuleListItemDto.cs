using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Alerting;

public sealed record AlertRuleListItemDto(
    Guid Id,
    string Name,
    AlertRuleKind Kind,
    AlertSeverity Severity,
    string Condition,
    string? ServerName,
    bool IsEnabled,
    bool NotifyRecovery,
    int RepeatIntervalMinutes,
    IReadOnlyList<string> ChannelNames,
    int FiringCount);
