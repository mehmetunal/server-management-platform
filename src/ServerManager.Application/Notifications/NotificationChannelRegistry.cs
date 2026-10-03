using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Application.Plugins;

namespace ServerManager.Application.Notifications;

public sealed class NotificationChannelRegistry : INotificationChannelRegistry
{
    private readonly IEnumerable<INotificationChannelProvider> _providers;
    private readonly IPluginCatalog _catalog;

    public NotificationChannelRegistry(IEnumerable<INotificationChannelProvider> providers, IPluginCatalog catalog)
    {
        _providers = providers;
        _catalog = catalog;
    }

    public IReadOnlyList<INotificationChannelProvider> GetEnabled() =>
        _providers
            .Where(provider => _catalog.IsAssemblyEnabled(provider.GetType().Assembly))
            .OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public INotificationChannelProvider? Find(string systemName) =>
        GetEnabled().FirstOrDefault(provider => string.Equals(provider.SystemName, systemName, StringComparison.OrdinalIgnoreCase));

    public string? FindDisplayName(string systemName) =>
        _providers.FirstOrDefault(provider => string.Equals(provider.SystemName, systemName, StringComparison.OrdinalIgnoreCase))?.DisplayName;
}
