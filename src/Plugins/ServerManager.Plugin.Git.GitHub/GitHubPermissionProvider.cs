using ServerManager.Application.Authorization;

namespace ServerManager.Plugin.Git.GitHub;

public sealed class GitHubPermissionProvider : IPermissionProvider
{
    public IReadOnlyList<PermissionDefinition> GetPermissions() =>
    [
        new(GitHubPermissions.Manage, "GitHub App oluşturma, ekleme, kurulumları görme ve kaldırma")
    ];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetDefaultRolePermissions() => new Dictionary<string, IReadOnlyList<string>>
    {
        [Roles.Admin] = [GitHubPermissions.Manage]
    };
}
