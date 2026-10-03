using System.Reflection;
using ServerManager.Application.Plugins;

namespace ServerManager.Application.Tests.Plugins;

public static class PluginTestData
{
    public const string SystemName = "Test.Sample";

    public static Assembly PluginAssembly => typeof(PluginTestData).Assembly;

    public static PluginDescriptor Descriptor(string systemName = SystemName, string group = "DevOps", int displayOrder = 0, string version = "1.0.0") => new()
    {
        SystemName = systemName,
        FriendlyName = systemName.Split('.').Last(),
        Group = group,
        Version = version,
        DisplayOrder = displayOrder,
        AssemblyFileName = "ServerManager.Application.Tests.dll"
    };

    public static LoadedPlugin Loaded(string systemName = SystemName, string version = "1.0.0") =>
        new(Descriptor(systemName, version: version), "/plugins/" + systemName, PluginAssembly, null);

    public static LoadedPlugin Failed(string systemName, string error = "Eklenti yüklenemedi: test") =>
        new(Descriptor(systemName), "/plugins/" + systemName, null, error);
}
