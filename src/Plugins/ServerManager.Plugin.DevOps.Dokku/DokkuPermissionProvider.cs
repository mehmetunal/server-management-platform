using ServerManager.Application.Authorization;

namespace ServerManager.Plugin.DevOps.Dokku;

public sealed class DokkuPermissionProvider : IPermissionProvider
{
    public IReadOnlyList<PermissionDefinition> GetPermissions() =>
    [
        new(DokkuPermissions.View, "Dokku durumu ve uygulamaları görüntüleme"),
        new(DokkuPermissions.Manage, "Dokku kurma ve uygulamaları yeniden başlatma")
    ];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetDefaultRolePermissions() => new Dictionary<string, IReadOnlyList<string>>
    {
        [Roles.Admin] = [DokkuPermissions.View, DokkuPermissions.Manage],
        [Roles.Operator] = [DokkuPermissions.View],
        [Roles.Developer] = [DokkuPermissions.View],
        [Roles.Viewer] = [DokkuPermissions.View]
    };
}
