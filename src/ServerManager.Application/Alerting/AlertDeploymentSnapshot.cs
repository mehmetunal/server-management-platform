using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

public sealed record AlertDeploymentSnapshot(
    Guid ProjectId,
    string ProjectName,
    Guid ServerId,
    string ServerName,
    Guid DeploymentId,
    DeploymentStatus Status,
    string? FailureReason,
    DateTime? CompletedAt);
