using ServerManager.Application.Interfaces.Backups;
using ServerManager.Application.Plugins;

namespace ServerManager.Application.Backups;

public sealed class BackupStorageRegistry : IBackupStorageRegistry
{
    private readonly IEnumerable<IBackupStorageProvider> _providers;
    private readonly IPluginCatalog _catalog;

    public BackupStorageRegistry(IEnumerable<IBackupStorageProvider> providers, IPluginCatalog catalog)
    {
        _providers = providers;
        _catalog = catalog;
    }

    public IReadOnlyList<IBackupStorageProvider> GetEnabled() =>
        _providers
            .Where(provider => _catalog.IsAssemblyEnabled(provider.GetType().Assembly))
            .OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public IBackupStorageProvider? Find(string systemName) =>
        GetEnabled().FirstOrDefault(provider => string.Equals(provider.SystemName, systemName, StringComparison.OrdinalIgnoreCase));

    public string? FindDisplayName(string systemName) =>
        _providers.FirstOrDefault(provider => string.Equals(provider.SystemName, systemName, StringComparison.OrdinalIgnoreCase))?.DisplayName;
}
