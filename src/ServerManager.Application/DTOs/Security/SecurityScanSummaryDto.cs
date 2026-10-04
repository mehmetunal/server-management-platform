using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Security;

public sealed class SecurityScanSummaryDto
{
    public Guid Id { get; init; }

    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public SecurityScanTrigger Trigger { get; init; }

    public SecurityScanStatus Status { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public int? Score { get; init; }

    public int CriticalCount { get; init; }

    public int WarningCount { get; init; }

    public bool IsPrivileged { get; init; }

    public string? FailureReason { get; init; }

    public string? UserName { get; init; }
}
