using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

public sealed class ProjectDetailsDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public GitProvider GitProvider { get; init; }

    public string RepositoryUrl { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string? GitUsername { get; init; }

    public bool HasAccessToken { get; init; }

    /// <summary>Depo bir Git entegrasyonuyla bağlıysa eklentinin SystemName'i.</summary>
    public string? GitIntegration { get; init; }

    /// <summary>Entegrasyonun görünen adı; eklenti devre dışıysa null.</summary>
    public string? GitIntegrationName { get; init; }

    public string? GitRepository { get; init; }

    public string DeployPath { get; init; } = string.Empty;

    public DeploymentBuildType BuildType { get; init; }

    public string? ComposeFile { get; init; }

    public string? DockerfilePath { get; init; }

    public string? PortMappings { get; init; }

    public string? BuildCommand { get; init; }

    public string? DeployCommand { get; init; }

    public bool UseSudoForCommands { get; init; }

    public IReadOnlyList<string> EnvironmentKeys { get; init; } = [];

    /// <summary>Kayıtlı ortam değişkenleri master key ile çözülemedi.</summary>
    public bool EnvironmentUnreadable { get; init; }

    public Guid? RunningDeploymentId { get; init; }

    public DateTime CreatedAt { get; init; }

    public string? CreatedBy { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }

    public string ComposeProjectName { get; init; } = string.Empty;

    public string ContainerName { get; init; } = string.Empty;
}
