using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.TestData;

public static class ProjectTestData
{
    public const string Sha = "0123456789abcdef0123456789abcdef01234567";

    public static CreateProjectDto ValidCreateDto(Guid serverId) => new()
    {
        ServerId = serverId,
        Name = "Müşteri API",
        GitProvider = GitProvider.GitHub,
        RepositoryUrl = "https://github.com/acme/api.git",
        Branch = "main",
        DeployPath = "/srv/apps/api",
        BuildType = DeploymentBuildType.DockerCompose,
        ComposeFile = "docker-compose.yml"
    };

    public static UpdateProjectDto ValidUpdateDto(DeploymentProject project) => new()
    {
        Id = project.Id,
        ServerId = project.ServerId,
        Name = project.Name,
        GitProvider = project.GitProvider,
        RepositoryUrl = project.RepositoryUrl,
        Branch = project.Branch,
        DeployPath = project.DeployPath,
        BuildType = project.BuildType,
        ComposeFile = project.ComposeFile
    };

    public static DeploymentProject Project(Server server) => new()
    {
        ServerId = server.Id,
        Server = server,
        Name = "Müşteri API",
        Slug = "musteri-api",
        RepositoryUrl = "https://github.com/acme/api.git",
        Branch = "main",
        DeployPath = "/srv/apps/api",
        BuildType = DeploymentBuildType.DockerCompose,
        ComposeFile = "docker-compose.yml"
    };

    public static Server Server() => new() { Name = "web-01", Hostname = "web-01", IpAddress = "203.0.113.10", Username = "deploy" };
}
