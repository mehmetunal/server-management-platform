using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

public sealed record AlertUptimeSnapshot(
    Guid Id,
    string Name,
    Guid? ServerId,
    string? ServerName,
    UptimeStatus Status,
    DateTime? StatusChangedAt,
    int ConsecutiveFailures,
    string? LastError);
