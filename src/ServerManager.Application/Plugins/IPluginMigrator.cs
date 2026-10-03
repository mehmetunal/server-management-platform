using System.Reflection;

namespace ServerManager.Application.Plugins;

/// <summary>Eklenti assembly'sindeki FluentMigrator migration'larını uygular.</summary>
public interface IPluginMigrator
{
    void MigrateUp(Assembly assembly);
}
