using System.Reflection;

namespace ServerManager.Application.Plugins;

/// <summary>Açılışta yüklenen eklentiler ve kurulum/etkinlik durumları.</summary>
public interface IPluginCatalog
{
    IReadOnlyList<LoadedPlugin> Plugins { get; }

    IEnumerable<Assembly> LoadedAssemblies { get; }

    LoadedPlugin? Find(string systemName);

    /// <summary>Tip veya bileşen bir eklentiye aitse o eklenti; çekirdek uygulamaya aitse null.</summary>
    LoadedPlugin? FindByAssembly(Assembly assembly);

    bool IsInstalled(string systemName);

    /// <summary>Kurulu ve etkin eklentiler için true.</summary>
    bool IsEnabled(string systemName);

    /// <summary>Çekirdek bileşenler için her zaman true; eklenti bileşenleri eklenti etkinse true.</summary>
    bool IsAssemblyEnabled(Assembly assembly);

    void SetState(string systemName, bool isEnabled);
}
