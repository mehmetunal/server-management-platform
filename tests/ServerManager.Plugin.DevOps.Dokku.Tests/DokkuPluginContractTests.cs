using System.Text.Json;
using ServerManager.Application.Authorization;
using ServerManager.Application.Plugins;
using ServerManager.Plugin.DevOps.Dokku;

namespace ServerManager.Plugin.DevOps.Dokku.Tests;

public class DokkuPluginContractTests
{
    [Fact]
    public void Permission_names_are_stable()
    {
        var permissions = new DokkuPermissionProvider().GetPermissions().Select(p => p.Name);

        Assert.Equal(["dokku.view", "dokku.manage"], permissions);
    }

    [Fact]
    public void Default_roles_include_view_and_admin_can_manage()
    {
        var matrix = new DokkuPermissionProvider().GetDefaultRolePermissions();

        Assert.Equal([DokkuPermissions.View, DokkuPermissions.Manage], matrix[Roles.Admin]);
        Assert.Equal([DokkuPermissions.View], matrix[Roles.Operator]);
        Assert.Equal([DokkuPermissions.View], matrix[Roles.Developer]);
        Assert.Equal([DokkuPermissions.View], matrix[Roles.Viewer]);
        Assert.False(matrix.ContainsKey(Roles.SuperAdmin));
    }

    [Fact]
    public void Audit_action_names_are_stable_and_labelled()
    {
        var names = new DokkuAuditActionProvider().GetDisplayNames();

        Assert.Equal(["dokku.install_start", "dokku.install_complete", "dokku.app_restart"], names.Keys);
        Assert.All(names.Values, label => Assert.False(string.IsNullOrWhiteSpace(label)));
    }

    [Fact]
    public void Server_tab_requires_view_permission()
    {
        var tab = Assert.Single(new DokkuServerTabProvider().GetTabs());

        Assert.Equal(DokkuPlugin.ServerTabKey, tab.Key);
        Assert.Equal(DokkuPermissions.View, tab.Permission);
        Assert.Equal("Dokku", tab.Controller);
    }

    [Fact]
    public void Descriptor_matches_plugin_assembly_and_logo()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, PluginDescriptor.FileName));
        var descriptor = JsonSerializer.Deserialize<PluginDescriptor>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(DokkuPlugin.SystemName, descriptor.SystemName);
        Assert.Equal(Path.GetFileName(typeof(DokkuPlugin).Assembly.Location), descriptor.AssemblyFileName);
        Assert.Equal("logo.svg", descriptor.Logo);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Version));
    }
}
