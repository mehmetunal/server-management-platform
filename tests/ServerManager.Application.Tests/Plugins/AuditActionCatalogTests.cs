using NSubstitute;
using ServerManager.Application.Auditing;

namespace ServerManager.Application.Tests.Plugins;

public class AuditActionCatalogTests
{
    [Fact]
    public void Merges_core_and_plugin_actions()
    {
        var provider = Substitute.For<IAuditActionProvider>();
        provider.GetDisplayNames().Returns(new Dictionary<string, string> { ["sample.deploy"] = "Örnek dağıtım" });

        var catalog = new AuditActionCatalog([provider]);

        Assert.Equal("Örnek dağıtım", catalog.DisplayName("sample.deploy"));
        Assert.Equal(AuditActions.DisplayNames[AuditActions.ServerCreate], catalog.DisplayName(AuditActions.ServerCreate));
        Assert.Contains(AuditActions.PluginInstall, catalog.DisplayNames.Keys);
    }

    [Fact]
    public void Plugin_cannot_rename_core_action()
    {
        var provider = Substitute.For<IAuditActionProvider>();
        provider.GetDisplayNames().Returns(new Dictionary<string, string> { [AuditActions.ServerCreate] = "Başka ad" });

        var catalog = new AuditActionCatalog([provider]);

        Assert.Equal(AuditActions.DisplayNames[AuditActions.ServerCreate], catalog.DisplayName(AuditActions.ServerCreate));
    }

    [Fact]
    public void Unknown_action_falls_back_to_its_code()
    {
        var catalog = new AuditActionCatalog([]);

        Assert.Equal("removed.plugin_action", catalog.DisplayName("removed.plugin_action"));
    }
}
