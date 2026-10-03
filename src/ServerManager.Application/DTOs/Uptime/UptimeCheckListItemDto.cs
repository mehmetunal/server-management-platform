using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Uptime;

public sealed record UptimeCheckListItemDto(
    Guid Id,
    string Name,
    UptimeCheckType Type,
    string Target,
    Guid? ServerId,
    string? ServerName,
    int IntervalSeconds,
    bool IsEnabled,
    UptimeStatus Status,
    DateTime? StatusChangedAt,
    DateTime? LastCheckedAt,
    int? LastResponseMs,
    int? LastStatusCode,
    string? LastError,
    int ConsecutiveFailures,
    double? UptimePercent24h);
