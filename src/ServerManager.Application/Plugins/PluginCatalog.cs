using System.Collections.Concurrent;
using System.Reflection;

namespace ServerManager.Application.Plugins;

public sealed class PluginCatalog : IPluginCatalog
{
    private readonly Dictionary<string, LoadedPlugin> _bySystemName;
    private readonly Dictionary<Assembly, LoadedPlugin> _byAssembly;
    private readonly ConcurrentDictionary<string, bool> _states = new(StringComparer.OrdinalIgnoreCase);

    /// <remarks>Reddedilen kopyalar (aynı SystemName) listede hatasıyla kalır; aramalarda yüklenen eklenti döner.</remarks>
    public PluginCatalog(IEnumerable<LoadedPlugin> plugins)
    {
        Plugins = plugins
            .OrderBy(p => p.Descriptor.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Descriptor.DisplayOrder)
            .ThenBy(p => p.Descriptor.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _bySystemName = new Dictionary<string, LoadedPlugin>(StringComparer.OrdinalIgnoreCase);
        _byAssembly = [];
        foreach (var plugin in Plugins.OrderByDescending(p => p.IsLoaded))
        {
            _bySystemName.TryAdd(plugin.SystemName, plugin);
            if (plugin.Assembly is not null)
                _byAssembly.TryAdd(plugin.Assembly, plugin);
        }
    }

    public IReadOnlyList<LoadedPlugin> Plugins { get; }

    public IEnumerable<Assembly> LoadedAssemblies => _byAssembly.Keys;

    public LoadedPlugin? Find(string systemName) =>
        _bySystemName.GetValueOrDefault(systemName);

    public LoadedPlugin? FindByAssembly(Assembly assembly) =>
        _byAssembly.GetValueOrDefault(assembly);

    public bool IsInstalled(string systemName) =>
        _states.ContainsKey(systemName);

    public bool IsEnabled(string systemName) =>
        _states.TryGetValue(systemName, out var enabled) && enabled;

    public bool IsAssemblyEnabled(Assembly assembly)
    {
        var plugin = FindByAssembly(assembly);
        return plugin is null || IsEnabled(plugin.SystemName);
    }

    public void SetState(string systemName, bool isEnabled) =>
        _states[systemName] = isEnabled;
}
