using ServerManager.Application.Interfaces.Repositories;

namespace ServerManager.Application.ServerSystem;

/// <summary>Sunucudaki panel projelerini ve yönetilen servislerini Docker kaynaklarıyla eşleştirmek için yükler.</summary>
internal static class PanelResourceContextLoader
{
    public static async Task<PanelResourceContext> LoadAsync(
        IDeploymentRepository deployments, IManagedServiceRepository services, Guid serverId, CancellationToken cancellationToken)
    {
        var projects = await deployments.GetProjectsByServerAsync(serverId, cancellationToken);
        var managed = await services.ListAsync(serverId, cancellationToken);

        var projectMap = new Dictionary<string, PanelResourceRef>(StringComparer.Ordinal);
        foreach (var project in projects.Where(p => !p.IsDeleted && p.Slug.Length > 0))
            projectMap.TryAdd(project.Slug, new PanelResourceRef(project.Id, project.Name));

        var serviceMap = new Dictionary<string, PanelResourceRef>(StringComparer.Ordinal);
        foreach (var service in managed.Where(s => !s.IsDeleted && s.Slug.Length > 0))
            serviceMap.TryAdd(service.Slug, new PanelResourceRef(service.Id, service.Name));

        return new PanelResourceContext(projectMap, serviceMap);
    }
}
