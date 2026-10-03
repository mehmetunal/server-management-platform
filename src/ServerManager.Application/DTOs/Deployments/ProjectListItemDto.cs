using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

public sealed class ProjectListItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public GitProvider GitProvider { get; init; }

    public string RepositoryUrl { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public DeploymentBuildType BuildType { get; init; }

    public DeploymentListItemDto? LastDeployment { get; set; }
}
