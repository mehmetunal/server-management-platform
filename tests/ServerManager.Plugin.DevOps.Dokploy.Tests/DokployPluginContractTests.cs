using System.Text.Json;
using ServerManager.Application.Authorization;
using ServerManager.Application.Plugins;

namespace ServerManager.Plugin.DevOps.Dokploy.Tests;

/// <summary>Rollerdeki izin claim'leri ve audit kayıtları bu adlarla saklanır; değişirse mevcut veriler eşleşmez.</summary>
public class DokployPluginContractTests
{
    [Fact]
    public void Permission_names_are_stable()
    {
        var permissions = new DokployPermissionProvider().GetPermissions().Select(p => p.Name);

        Assert.Equal(["dokploy.view", "dokploy.install", "dokploy.manage"], permissions);
    }

    [Fact]
    public void Default_roles_match_previous_core_matrix()
    {
        var matrix = new DokployPermissionProvider().GetDefaultRolePermissions();

        Assert.Equal([DokployPermissions.View, DokployPermissions.Install, DokployPermissions.Manage], matrix[Roles.Admin]);
        Assert.Equal([DokployPermissions.View], matrix[Roles.Operator]);
        Assert.Equal([DokployPermissions.View], matrix[Roles.Developer]);
        Assert.Equal([DokployPermissions.View], matrix[Roles.Viewer]);
        Assert.False(matrix.ContainsKey(Roles.SuperAdmin));
    }

    [Fact]
    public void Audit_action_names_are_stable_and_labelled()
    {
        var names = new DokployAuditActionProvider().GetDisplayNames();

        Assert.Equal(
            ["dokploy.install_start", "dokploy.install_complete", "dokploy.detected", "dokploy.settings_update", "dokploy.api_key_remove"],
            names.Keys);
        Assert.All(names.Values, label => Assert.False(string.IsNullOrWhiteSpace(label)));
    }

    [Fact]
    public void Server_tab_requires_view_permission()
    {
        var tab = Assert.Single(new DokployServerTabProvider().GetTabs());

        Assert.Equal(DokployPlugin.ServerTabKey, tab.Key);
        Assert.Equal(DokployPermissions.View, tab.Permission);
        Assert.Equal("Dokploy", tab.Controller);
    }

    [Fact]
    public void Descriptor_matches_plugin_assembly()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, PluginDescriptor.FileName));
        var descriptor = JsonSerializer.Deserialize<PluginDescriptor>(json)!;

        Assert.Equal(DokployPlugin.SystemName, descriptor.SystemName);
        Assert.Equal(Path.GetFileName(typeof(DokployPlugin).Assembly.Location), descriptor.AssemblyFileName);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Version));
    }
}
