using ServerManager.Application.Authorization;

namespace ServerManager.Plugin.DevOps.Dokploy;

public sealed class DokployPermissionProvider : IPermissionProvider
{
    public IReadOnlyList<PermissionDefinition> GetPermissions() =>
    [
        new(DokployPermissions.View, "Dokploy durumu, projeler ve kurulum geçmişi görüntüleme"),
        new(DokployPermissions.Install, "Sunucuya Dokploy kurma"),
        new(DokployPermissions.Manage, "Dokploy bağlantı ayarları ve API anahtarı yönetimi")
    ];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetDefaultRolePermissions() => new Dictionary<string, IReadOnlyList<string>>
    {
        [Roles.Admin] = [DokployPermissions.View, DokployPermissions.Install, DokployPermissions.Manage],
        [Roles.Operator] = [DokployPermissions.View],
        [Roles.Developer] = [DokployPermissions.View],
        [Roles.Viewer] = [DokployPermissions.View]
    };
}
