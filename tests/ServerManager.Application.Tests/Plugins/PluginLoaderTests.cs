using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Application.Tests.Plugins;

public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sm-plugin-tests-" + Guid.NewGuid().ToString("N"));

    public PluginLoaderTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string CreatePluginFolder(string folder, string? json, bool copyAssembly = false)
    {
        var directory = Path.Combine(_root, folder);
        Directory.CreateDirectory(directory);
        if (json is not null)
            File.WriteAllText(Path.Combine(directory, "plugin.json"), json);
        if (copyAssembly)
            File.Copy(PluginTestData.PluginAssembly.Location, Path.Combine(directory, Path.GetFileName(PluginTestData.PluginAssembly.Location)));
        return directory;
    }

    private static string Descriptor(string systemName, string assemblyFileName = "ServerManager.Application.Tests.dll") =>
        $$"""
        {
          // yorumlara ve sondaki virgüle izin verilir
          "systemName": "{{systemName}}",
          "FriendlyName": "Örnek",
          "Group": "DevOps",
          "Version": "1.2.0",
          "AssemblyFileName": "{{assemblyFileName}}",
        }
        """;

    [Fact]
    public void Missing_directory_yields_empty_catalog()
    {
        var result = PluginLoader.Load(Path.Combine(_root, "yok"));

        Assert.Empty(result.Catalog.Plugins);
        Assert.Empty(result.Startups);
    }

    [Fact]
    public void Folders_without_descriptor_are_ignored()
    {
        CreatePluginFolder("Empty", json: null);

        Assert.Empty(PluginLoader.Load(_root).Catalog.Plugins);
    }

    [Fact]
    public void Valid_plugin_is_loaded_and_its_startup_is_discovered()
    {
        CreatePluginFolder("Test.Sample", Descriptor("Test.Sample"), copyAssembly: true);

        var result = PluginLoader.Load(_root);

        var plugin = Assert.Single(result.Catalog.Plugins);
        Assert.True(plugin.IsLoaded, plugin.LoadError);
        Assert.Equal("1.2.0", plugin.Descriptor.Version);
        Assert.Same(PluginTestData.PluginAssembly, plugin.Assembly);
        Assert.Contains(result.Startups, s => s is TestPluginStartup);
    }

    [Fact]
    public void Unreadable_descriptor_is_reported()
    {
        CreatePluginFolder("Broken", "{ not json");

        var plugin = Assert.Single(PluginLoader.Load(_root).Catalog.Plugins);

        Assert.False(plugin.IsLoaded);
        Assert.Equal("Broken", plugin.SystemName);
        Assert.Contains("plugin.json okunamadı", plugin.LoadError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("DevOps/../Evil")]
    [InlineData("1Plugin")]
    [InlineData("DevOps..Dokku")]
    public void Invalid_system_name_is_rejected(string systemName)
    {
        CreatePluginFolder("Invalid", Descriptor(systemName), copyAssembly: true);

        var plugin = Assert.Single(PluginLoader.Load(_root).Catalog.Plugins);

        Assert.False(plugin.IsLoaded);
        Assert.Contains("SystemName", plugin.LoadError);
    }

    [Fact]
    public void Duplicate_system_name_is_rejected()
    {
        CreatePluginFolder("A", Descriptor("Test.Sample"), copyAssembly: true);
        CreatePluginFolder("B", Descriptor("test.sample"), copyAssembly: true);

        var plugins = PluginLoader.Load(_root).Catalog.Plugins;

        Assert.Equal(2, plugins.Count);
        Assert.Single(plugins, p => p.IsLoaded);
        Assert.Single(plugins, p => p.LoadError?.Contains("Aynı SystemName") == true);
    }

    [Fact]
    public void Two_plugins_cannot_share_an_assembly()
    {
        CreatePluginFolder("A", Descriptor("Test.First"), copyAssembly: true);
        CreatePluginFolder("B", Descriptor("Test.Second"), copyAssembly: true);

        var result = PluginLoader.Load(_root);

        Assert.True(result.Catalog.Find("Test.First")!.IsLoaded);
        Assert.Contains("başka bir eklenti", result.Catalog.Find("Test.Second")!.LoadError);
        Assert.Single(result.Startups, s => s is TestPluginStartup);
    }

    [Theory]
    [InlineData("Missing.dll")]
    [InlineData("../ServerManager.Application.Tests.dll")]
    [InlineData("")]
    public void Assembly_must_exist_inside_the_plugin_folder(string assemblyFileName)
    {
        CreatePluginFolder("Outside", Descriptor("Test.Outside", assemblyFileName));
        File.Copy(PluginTestData.PluginAssembly.Location, Path.Combine(_root, "ServerManager.Application.Tests.dll"));

        var plugin = Assert.Single(PluginLoader.Load(_root).Catalog.Plugins);

        Assert.False(plugin.IsLoaded);
        Assert.Contains("assembly'si bulunamadı", plugin.LoadError);
    }
}
