using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

public abstract class ProjectFormDto
{
    public Guid ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Boşsa depo adresiyle çalışılır; doluysa <c>GitSourceKeys</c> biçiminde entegrasyon bağlantısıdır.</summary>
    public string? GitSource { get; set; }

    /// <summary>Entegrasyondaki depo adı (<see cref="GitSource"/> doluyken zorunlu).</summary>
    public string? GitRepository { get; set; }

    public GitProvider GitProvider { get; set; } = GitProvider.GitHub;

    public string RepositoryUrl { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";

    public string? GitUsername { get; set; }

    public string? AccessToken { get; set; }

    public string DeployPath { get; set; } = string.Empty;

    public DeploymentBuildType BuildType { get; set; } = DeploymentBuildType.DockerCompose;

    public string? ComposeFile { get; set; } = "docker-compose.yml";

    public string? DockerfilePath { get; set; } = "Dockerfile";

    public string? PortMappings { get; set; }

    public string? BuildCommand { get; set; }

    public string? DeployCommand { get; set; }

    public bool UseSudoForCommands { get; set; }

    public string? Environment { get; set; }

    public bool UsesIntegration => !string.IsNullOrWhiteSpace(GitSource);

    public void ClearSecrets()
    {
        AccessToken = null;
        Environment = null;
    }
}
