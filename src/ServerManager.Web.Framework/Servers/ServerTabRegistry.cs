using ServerManager.Application.Plugins;

namespace ServerManager.Web.Framework.Servers;

/// <summary>Etkin eklentilerin sunucu sekmeleri; devre dışı eklentinin sekmesi gösterilmez.</summary>
public sealed class ServerTabRegistry
{
    private readonly IEnumerable<IServerTabProvider> _providers;
    private readonly IPluginCatalog _catalog;

    public ServerTabRegistry(IEnumerable<IServerTabProvider> providers, IPluginCatalog catalog)
    {
        _providers = providers;
        _catalog = catalog;
    }

    public IReadOnlyList<ServerTab> GetTabs() =>
        _providers
            .Where(provider => _catalog.IsAssemblyEnabled(provider.GetType().Assembly))
            .SelectMany(provider => provider.GetTabs())
            .OrderBy(tab => tab.Order)
            .ThenBy(tab => tab.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
