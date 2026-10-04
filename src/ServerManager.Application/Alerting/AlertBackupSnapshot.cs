using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

public sealed record AlertBackupSnapshot(
    Guid JobId,
    string JobName,
    Guid ServerId,
    string ServerName,
    Guid RunId,
    BackupRunStatus Status,
    string? FailureReason,
    DateTime? CompletedAt);
