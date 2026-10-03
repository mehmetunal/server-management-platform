using ServerManager.Application.Plugins;

namespace ServerManager.Application.Tests.Plugins;

public class PluginCatalogTests
{
    [Fact]
    public void Plugins_are_ordered_by_group_display_order_and_name()
    {
        var catalog = new PluginCatalog(
        [
            new LoadedPlugin(PluginTestData.Descriptor("DevOps.Zeta", "DevOps", 2), "/z", null, "x"),
            new LoadedPlugin(PluginTestData.Descriptor("Backup.Restic", "Backup", 5), "/r", null, "x"),
            new LoadedPlugin(PluginTestData.Descriptor("DevOps.Alpha", "DevOps", 2), "/a", null, "x"),
            new LoadedPlugin(PluginTestData.Descriptor("DevOps.First", "DevOps", 1), "/f", null, "x")
        ]);

        Assert.Equal(["Backup.Restic", "DevOps.First", "DevOps.Alpha", "DevOps.Zeta"], catalog.Plugins.Select(p => p.SystemName));
    }

    [Fact]
    public void Find_is_case_insensitive()
    {
        var catalog = new PluginCatalog([PluginTestData.Loaded()]);

        Assert.NotNull(catalog.Find(PluginTestData.SystemName.ToUpperInvariant()));
        Assert.Null(catalog.Find("Other.Plugin"));
    }

    [Fact]
    public void Plugin_is_neither_installed_nor_enabled_until_state_is_set()
    {
        var catalog = new PluginCatalog([PluginTestData.Loaded()]);

        Assert.False(catalog.IsInstalled(PluginTestData.SystemName));
        Assert.False(catalog.IsEnabled(PluginTestData.SystemName));
        Assert.False(catalog.IsAssemblyEnabled(PluginTestData.PluginAssembly));
    }

    [Fact]
    public void Disabled_plugin_stays_installed()
    {
        var catalog = new PluginCatalog([PluginTestData.Loaded()]);

        catalog.SetState(PluginTestData.SystemName, true);
        Assert.True(catalog.IsEnabled(PluginTestData.SystemName));
        Assert.True(catalog.IsAssemblyEnabled(PluginTestData.PluginAssembly));

        catalog.SetState(PluginTestData.SystemName, false);
        Assert.True(catalog.IsInstalled(PluginTestData.SystemName));
        Assert.False(catalog.IsEnabled(PluginTestData.SystemName));
        Assert.False(catalog.IsAssemblyEnabled(PluginTestData.PluginAssembly));
    }

    [Fact]
    public void Core_assemblies_are_always_enabled()
    {
        var catalog = new PluginCatalog([PluginTestData.Loaded()]);

        Assert.True(catalog.IsAssemblyEnabled(typeof(PluginCatalog).Assembly));
        Assert.Null(catalog.FindByAssembly(typeof(PluginCatalog).Assembly));
    }

    [Fact]
    public void Only_loaded_plugins_expose_assemblies()
    {
        var catalog = new PluginCatalog([PluginTestData.Loaded(), PluginTestData.Failed("DevOps.Broken")]);

        Assert.Equal([PluginTestData.PluginAssembly], catalog.LoadedAssemblies);
        Assert.Same(catalog.Find(PluginTestData.SystemName), catalog.FindByAssembly(PluginTestData.PluginAssembly));
    }
}
