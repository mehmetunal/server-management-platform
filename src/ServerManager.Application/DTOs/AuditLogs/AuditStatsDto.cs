namespace ServerManager.Application.DTOs.AuditLogs;

public sealed class AuditStatsDto
{
    public DateTime SinceUtc { get; init; }

    public int TotalCount { get; init; }

    public int FailedCount { get; init; }

    public int ActiveUserCount { get; init; }

    public int FailedLoginCount { get; init; }

    public IReadOnlyList<AuditActionCount> TopActions { get; init; } = [];
}
