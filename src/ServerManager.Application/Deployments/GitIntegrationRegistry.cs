using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Plugins;

namespace ServerManager.Application.Deployments;

public sealed class GitIntegrationRegistry : IGitIntegrationRegistry
{
    private readonly IEnumerable<IGitIntegration> _integrations;
    private readonly IPluginCatalog _catalog;

    public GitIntegrationRegistry(IEnumerable<IGitIntegration> integrations, IPluginCatalog catalog)
    {
        _integrations = integrations;
        _catalog = catalog;
    }

    public IReadOnlyList<IGitIntegration> GetEnabled() =>
        _integrations
            .Where(integration => _catalog.IsAssemblyEnabled(integration.GetType().Assembly))
            .OrderBy(integration => integration.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public IGitIntegration? Find(string systemName) =>
        GetEnabled().FirstOrDefault(integration => string.Equals(integration.SystemName, systemName, StringComparison.OrdinalIgnoreCase));
}
