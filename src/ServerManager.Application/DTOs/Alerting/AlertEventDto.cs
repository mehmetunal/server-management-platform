using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Alerting;

public sealed record AlertEventDto(
    Guid Id,
    Guid RuleId,
    string RuleName,
    AlertRuleKind Kind,
    AlertSeverity Severity,
    Guid? ServerId,
    string? ServerName,
    string TargetName,
    AlertEventStatus Status,
    string Message,
    DateTime StartedAt,
    DateTime? ResolvedAt,
    string? ResolvedMessage,
    DateTime? AcknowledgedAt,
    string? AcknowledgedBy);
