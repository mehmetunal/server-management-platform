using ServerManager.Application.Authorization;

namespace ServerManager.Application.Tests.Plugins;

/// <summary>Test assembly'si eklenti assembly'si gibi kullanıldığında eklentiye ait izin sağlayıcısı.</summary>
public sealed class TestPluginPermissionProvider : IPermissionProvider
{
    public const string View = "testplugin.view";
    public const string Manage = "testplugin.manage";

    public IReadOnlyList<PermissionDefinition> GetPermissions() =>
    [
        new(View, "Test eklentisini görüntüleme"),
        new(Manage, "Test eklentisini yönetme")
    ];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetDefaultRolePermissions() => new Dictionary<string, IReadOnlyList<string>>
    {
        [Roles.Admin] = [View, Manage],
        [Roles.Viewer] = [View]
    };
}
