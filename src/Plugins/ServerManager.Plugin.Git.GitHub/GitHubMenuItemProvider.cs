using ServerManager.Web.Framework.Navigation;

namespace ServerManager.Plugin.Git.GitHub;

public sealed class GitHubMenuItemProvider : IMenuItemProvider
{
    public IEnumerable<MenuItem> GetItems() =>
    [
        new(MenuGroups.Deployment, "GitHub", "GitHub App bağlantıları ve kurulu hesaplar", "link", GitHubPermissions.Manage, "GitHub")
    ];
}
