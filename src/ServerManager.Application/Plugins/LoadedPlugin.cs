using System.Reflection;

namespace ServerManager.Application.Plugins;

/// <summary>Açılışta bulunan eklenti. Yüklenemeyen eklenti listede hatasıyla görünür, çalıştırılmaz.</summary>
public sealed class LoadedPlugin
{
    public LoadedPlugin(PluginDescriptor descriptor, string directory, Assembly? assembly, string? loadError)
    {
        Descriptor = descriptor;
        Directory = directory;
        Assembly = assembly;
        LoadError = loadError;
    }

    public PluginDescriptor Descriptor { get; }

    public string Directory { get; }

    public Assembly? Assembly { get; }

    public string? LoadError { get; }

    public bool IsLoaded => Assembly is not null;

    public string SystemName => Descriptor.SystemName;
}
