using System.Security.Claims;
using ServerManager.Application.Plugins;
using ServerManager.Web.Framework.Authorization;

namespace ServerManager.Web.Framework.Navigation;

/// <summary>Etkin eklentilerin menü bağlantıları; devre dışı eklentinin veya kullanıcının izni olmayan bağlantı gösterilmez.</summary>
public sealed class MenuRegistry
{
    private readonly IEnumerable<IMenuItemProvider> _providers;
    private readonly IPluginCatalog _catalog;

    public MenuRegistry(IEnumerable<IMenuItemProvider> providers, IPluginCatalog catalog)
    {
        _providers = providers;
        _catalog = catalog;
    }

    public IReadOnlyList<MenuItem> GetItems(string group, ClaimsPrincipal user) =>
        _providers
            .Where(provider => _catalog.IsAssemblyEnabled(provider.GetType().Assembly))
            .SelectMany(provider => provider.GetItems())
            .Where(item => string.Equals(item.Group, group, StringComparison.Ordinal) && user.HasPermission(item.Permission))
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
