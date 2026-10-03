using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

public class DeploymentListItemDto
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string? CommitSha { get; init; }

    public string? CommitMessage { get; init; }

    public DeploymentStatus Status { get; init; }

    public string? FailureReason { get; init; }

    public string? UserName { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public bool IsRunning => Status is DeploymentStatus.Started or DeploymentStatus.Building or DeploymentStatus.Deploying;
}
