using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

public sealed class DeploymentPlan
{
    public required string Slug { get; init; }

    public required GitSource Source { get; init; }

    public required string Branch { get; init; }

    /// <summary>Boşsa dalın son commit'i alınır.</summary>
    public string? Commit { get; init; }

    public required string DeployPath { get; init; }

    public DeploymentBuildType BuildType { get; init; }

    public string ComposeFile { get; init; } = "docker-compose.yml";

    public string DockerfilePath { get; init; } = "Dockerfile";

    public IReadOnlyList<string> PortMappings { get; init; } = [];

    public string? BuildCommand { get; init; }

    public string? DeployCommand { get; init; }

    public bool UseSudoForCommands { get; init; }

    /// <summary>Proje klasörüne <c>.env</c> olarak yazılır; null ise mevcut dosyaya dokunulmaz.</summary>
    public string? Environment { get; init; }

    public TimeSpan GitTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan BuildTimeout { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan DeployTimeout { get; init; } = TimeSpan.FromMinutes(10);
}
