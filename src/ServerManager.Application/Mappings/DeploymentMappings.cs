using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Mappings;

public static class DeploymentMappings
{
    public static ProjectListItemDto ToListItemDto(this DeploymentProject project, Deployment? lastDeployment) => new()
    {
        Id = project.Id,
        Name = project.Name,
        ServerId = project.ServerId,
        ServerName = project.Server?.Name ?? string.Empty,
        GitProvider = project.GitProvider,
        RepositoryUrl = project.RepositoryUrl,
        Branch = project.Branch,
        BuildType = project.BuildType,
        LastDeployment = lastDeployment?.ToListItemDto()
    };

    public static ProjectDetailsDto ToDetailsDto(
        this DeploymentProject project,
        IReadOnlyList<string> environmentKeys,
        bool environmentUnreadable,
        Guid? runningDeploymentId,
        string? gitIntegrationName = null) => new()
    {
        GitIntegration = project.GitIntegration,
        GitIntegrationName = gitIntegrationName,
        GitRepository = project.GitRepository,
        Id = project.Id,
        Name = project.Name,
        Slug = project.Slug,
        Description = project.Description,
        ServerId = project.ServerId,
        ServerName = project.Server?.Name ?? string.Empty,
        GitProvider = project.GitProvider,
        RepositoryUrl = project.RepositoryUrl,
        Branch = project.Branch,
        GitUsername = project.GitUsername,
        HasAccessToken = project.EncryptedAccessToken is not null,
        DeployPath = project.DeployPath,
        BuildType = project.BuildType,
        ComposeFile = project.ComposeFile,
        DockerfilePath = project.DockerfilePath,
        PortMappings = project.PortMappings,
        BuildCommand = project.BuildCommand,
        DeployCommand = project.DeployCommand,
        UseSudoForCommands = project.UseSudoForCommands,
        EnvironmentKeys = environmentKeys,
        EnvironmentUnreadable = environmentUnreadable,
        RunningDeploymentId = runningDeploymentId,
        CreatedAt = project.CreatedAt,
        CreatedBy = project.CreatedBy,
        UpdatedAt = project.UpdatedAt,
        UpdatedBy = project.UpdatedBy,
        ComposeProjectName = DeploymentNames.ComposeProjectName(project.Slug),
        ContainerName = DeploymentNames.ContainerName(project.Slug)
    };

    public static UpdateProjectDto ToUpdateDto(this DeploymentProject project, IReadOnlyList<string> environmentKeys) => new()
    {
        Id = project.Id,
        Slug = project.Slug,
        ServerId = project.ServerId,
        Name = project.Name,
        Description = project.Description,
        GitSource = project.GitIntegration is null || project.GitSourceId is null ? null : GitSourceKeys.Format(project.GitIntegration, project.GitSourceId),
        GitRepository = project.GitRepository,
        GitProvider = project.GitProvider,
        RepositoryUrl = project.RepositoryUrl,
        Branch = project.Branch,
        GitUsername = project.GitUsername,
        HasAccessToken = project.EncryptedAccessToken is not null,
        DeployPath = project.DeployPath,
        BuildType = project.BuildType,
        ComposeFile = project.ComposeFile ?? "docker-compose.yml",
        DockerfilePath = project.DockerfilePath ?? "Dockerfile",
        PortMappings = project.PortMappings,
        BuildCommand = project.BuildCommand,
        DeployCommand = project.DeployCommand,
        UseSudoForCommands = project.UseSudoForCommands,
        HasEnvironment = project.EncryptedEnvironment is not null,
        EnvironmentKeys = environmentKeys
    };

    public static DeploymentListItemDto ToListItemDto(this Deployment deployment) => new()
    {
        Id = deployment.Id,
        ProjectId = deployment.ProjectId,
        ProjectName = deployment.ProjectName,
        ServerId = deployment.ServerId,
        ServerName = deployment.ServerName,
        Branch = deployment.Branch,
        CommitSha = deployment.CommitSha,
        CommitMessage = deployment.CommitMessage,
        Status = deployment.Status,
        FailureReason = deployment.FailureReason,
        UserName = deployment.UserName,
        StartedAt = deployment.StartedAt,
        CompletedAt = deployment.CompletedAt
    };

    public static DeploymentDetailsDto ToDetailsDto(this Deployment deployment, DeploymentProject? project, bool includeLog) => new()
    {
        Id = deployment.Id,
        ProjectId = deployment.ProjectId,
        ProjectName = deployment.ProjectName,
        ServerId = deployment.ServerId,
        ServerName = deployment.ServerName,
        Branch = deployment.Branch,
        CommitSha = deployment.CommitSha,
        CommitMessage = deployment.CommitMessage,
        Status = deployment.Status,
        FailureReason = deployment.FailureReason,
        UserName = deployment.UserName,
        StartedAt = deployment.StartedAt,
        CompletedAt = deployment.CompletedAt,
        BuildType = deployment.BuildType,
        RequestedCommit = deployment.RequestedCommit,
        CommitAuthor = deployment.CommitAuthor,
        CommitUrl = project is null ? null : GitRepositoryUrls.CommitUrl(project.GitProvider, project.RepositoryUrl, deployment.CommitSha),
        SourceDeploymentId = deployment.SourceDeploymentId,
        ExitCode = deployment.ExitCode,
        IpAddress = deployment.IpAddress,
        BuildStartedAt = deployment.BuildStartedAt,
        DeployStartedAt = deployment.DeployStartedAt,
        CancelledBy = deployment.CancelledBy,
        Log = includeLog ? deployment.Log : string.Empty,
        ProjectExists = project is not null
    };

    public static bool IsRunning(this DeploymentStatus status) =>
        status is DeploymentStatus.Started or DeploymentStatus.Building or DeploymentStatus.Deploying;
}
