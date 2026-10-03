using System.Text.Json;
using ServerManager.Application.Authorization;
using ServerManager.Application.Plugins;
using ServerManager.Plugin.Git.GitHub.Integration;
using ServerManager.Web.Framework.Navigation;

namespace ServerManager.Plugin.Git.GitHub.Tests;

/// <summary>İzin, audit ve entegrasyon adları veritabanında saklanır; değişirse mevcut kayıtlar eşleşmez.</summary>
public class GitHubPluginContractTests
{
    [Fact]
    public void Permission_names_are_stable_and_admin_only_by_default()
    {
        var provider = new GitHubPermissionProvider();

        Assert.Equal(["github.manage"], provider.GetPermissions().Select(p => p.Name));
        var matrix = provider.GetDefaultRolePermissions();
        Assert.Equal([GitHubPermissions.Manage], matrix[Roles.Admin]);
        Assert.Single(matrix);
    }

    [Fact]
    public void Audit_action_names_are_stable_and_labelled()
    {
        var names = new GitHubAuditActionProvider().GetDisplayNames();

        Assert.Equal(["github.app_create", "github.app_delete"], names.Keys);
        Assert.All(names.Values, label => Assert.False(string.IsNullOrWhiteSpace(label)));
    }

    [Fact]
    public void Menu_item_is_in_deployment_group_and_requires_manage_permission()
    {
        var item = Assert.Single(new GitHubMenuItemProvider().GetItems());

        Assert.Equal(MenuGroups.Deployment, item.Group);
        Assert.Equal(GitHubPermissions.Manage, item.Permission);
        Assert.Equal("GitHub", item.Controller);
    }

    [Fact]
    public void Integration_name_is_the_plugin_system_name()
    {
        Assert.Equal("Git.GitHub", GitHubPlugin.SystemName);
        Assert.Equal("x-access-token", GitHubGitIntegration.TokenUsername);
    }

    [Fact]
    public void Descriptor_matches_plugin_assembly()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, PluginDescriptor.FileName));
        var descriptor = JsonSerializer.Deserialize<PluginDescriptor>(json)!;

        Assert.Equal(GitHubPlugin.SystemName, descriptor.SystemName);
        Assert.Equal(Path.GetFileName(typeof(GitHubPlugin).Assembly.Location), descriptor.AssemblyFileName);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Version));
    }
}
