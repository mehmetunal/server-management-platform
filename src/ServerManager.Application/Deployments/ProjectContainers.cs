using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Deployments;

/// <summary>Sunucudaki container listesinden projeye ait olanları seçer.</summary>
public static class ProjectContainers
{
    public static IReadOnlyList<ProjectContainerDto> Select(string slug, DeploymentBuildType buildType, IEnumerable<DockerContainerDto> containers)
    {
        var composeProject = DeploymentNames.ComposeProjectName(slug);
        var containerName = DeploymentNames.ContainerName(slug);
        return buildType switch
        {
            DeploymentBuildType.DockerCompose => containers
                .Where(c => string.Equals(c.ComposeProject, composeProject, StringComparison.Ordinal))
                .OrderBy(c => c.Name, StringComparer.Ordinal)
                .Select(c => Map(c, ServiceName(composeProject, c.Name)))
                .ToList(),
            DeploymentBuildType.Dockerfile => containers
                .Where(c => string.Equals(c.Name, containerName, StringComparison.Ordinal))
                .Select(c => Map(c, null))
                .ToList(),
            _ => []
        };
    }

    /// <summary>Compose'un varsayılan adından (<c>&lt;proje&gt;-&lt;servis&gt;-&lt;n&gt;</c>) servis adı; özel container_name ise adın kendisi.</summary>
    public static string ServiceName(string composeProject, string containerName)
    {
        var prefix = composeProject + "-";
        if (!containerName.StartsWith(prefix, StringComparison.Ordinal))
            return containerName;

        var rest = containerName[prefix.Length..];
        var dash = rest.LastIndexOf('-');
        return dash > 0 && rest[(dash + 1)..].All(char.IsAsciiDigit) ? rest[..dash] : rest;
    }

    private static ProjectContainerDto Map(DockerContainerDto container, string? service) => new()
    {
        Name = container.Name,
        Service = service,
        State = container.State,
        Status = container.Status,
        Image = container.Image
    };
}
