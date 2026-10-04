using ServerManager.Application.Interfaces.Cloud;
using ServerManager.Application.Plugins;

namespace ServerManager.Application.Cloud;

public sealed class CloudProviderRegistry : ICloudProviderRegistry
{
    private readonly IEnumerable<ICloudProvider> _providers;
    private readonly IPluginCatalog _catalog;

    public CloudProviderRegistry(IEnumerable<ICloudProvider> providers, IPluginCatalog catalog)
    {
        _providers = providers;
        _catalog = catalog;
    }

    public IReadOnlyList<ICloudProvider> GetEnabled() =>
        _providers
            .Where(provider => _catalog.IsAssemblyEnabled(provider.GetType().Assembly))
            .OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public ICloudProvider? Find(string systemName) =>
        GetEnabled().FirstOrDefault(provider => string.Equals(provider.SystemName, systemName, StringComparison.OrdinalIgnoreCase));

    public string? FindDisplayName(string systemName) =>
        _providers.FirstOrDefault(provider => string.Equals(provider.SystemName, systemName, StringComparison.OrdinalIgnoreCase))?.DisplayName;
}
