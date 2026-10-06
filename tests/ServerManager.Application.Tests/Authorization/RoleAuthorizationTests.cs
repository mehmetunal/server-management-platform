using NSubstitute;
using ServerManager.Application.Authorization;

namespace ServerManager.Application.Tests.Authorization;

public class PermissionSeedPlannerTests
{
    private static IReadOnlySet<string> Set(params string[] values) => values.ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Fresh_role_gets_all_defaults_and_marks_them_known()
    {
        var plan = PermissionSeedPlanner.For(Roles.Viewer, ["a", "b"], Set(), Set());

        Assert.Equal(["a", "b"], plan.ToGrant);
        Assert.Equal(["a", "b"], plan.ToMarkKnown);
    }

    [Fact]
    public void Permission_removed_by_admin_is_not_re_added()
    {
        // "b" daha önce önerildi (bilinen) ama yönetici rolden kaldırdı.
        var plan = PermissionSeedPlanner.For(Roles.Admin, ["a", "b"], Set("a"), Set("a", "b"));

        Assert.Empty(plan.ToGrant);
        Assert.Empty(plan.ToMarkKnown);
    }

    [Fact]
    public void Only_new_permissions_are_added_on_upgrade()
    {
        var plan = PermissionSeedPlanner.For(Roles.Admin, ["a", "b", "roles.manage"], Set("a"), Set("a", "b"));

        Assert.Equal(["roles.manage"], plan.ToGrant);
        Assert.Equal(["roles.manage"], plan.ToMarkKnown);
    }

    [Fact]
    public void Existing_permission_that_was_not_known_is_only_marked()
    {
        var plan = PermissionSeedPlanner.For(Roles.Operator, ["a"], Set("a"), Set());

        Assert.Empty(plan.ToGrant);
        Assert.Equal(["a"], plan.ToMarkKnown);
    }

    [Fact]
    public void SuperAdmin_always_gets_every_missing_permission()
    {
        var plan = PermissionSeedPlanner.For(Roles.SuperAdmin, ["a", "b"], Set("a"), Set("a", "b"));

        Assert.Equal(["b"], plan.ToGrant);
    }
}

public class RoleLockoutGuardTests
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private static Dictionary<string, IReadOnlySet<string>> Matrix(params (string Role, string[] Permissions)[] roles) =>
        roles.ToDictionary(r => r.Role, r => (IReadOnlySet<string>)r.Permissions.ToHashSet(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Removing_user_manage_from_last_role_with_active_user_is_blocked()
    {
        var users = new List<ActiveUserRoles> { new(Alice, ["Ops"]) };
        var before = Matrix(("Ops", [Permissions.UserManage, Permissions.RolesManage]));
        var after = Matrix(("Ops", [Permissions.RolesManage]));

        var lost = RoleLockoutGuard.LostPermissions(users, before, users, after);

        Assert.Equal([Permissions.UserManage], lost);
    }

    [Fact]
    public void Removal_is_allowed_when_another_active_user_keeps_the_permission()
    {
        var users = new List<ActiveUserRoles> { new(Alice, ["Ops"]), new(Bob, ["Leads"]) };
        var before = Matrix(("Ops", [Permissions.UserManage]), ("Leads", [Permissions.UserManage]));
        var after = Matrix(("Ops", []), ("Leads", [Permissions.UserManage]));

        Assert.Empty(RoleLockoutGuard.LostPermissions(users, before, users, after));
    }

    [Fact]
    public void Active_SuperAdmin_always_holds_critical_permissions()
    {
        var users = new List<ActiveUserRoles> { new(Alice, [Roles.SuperAdmin]), new(Bob, ["Ops"]) };
        var before = Matrix(("Ops", [Permissions.RolesManage]), (Roles.SuperAdmin, []));
        var after = Matrix(("Ops", []), (Roles.SuperAdmin, []));

        Assert.Empty(RoleLockoutGuard.LostPermissions(users, before, users, after));
    }

    [Fact]
    public void Deactivating_last_holder_is_blocked()
    {
        var matrix = Matrix(("Ops", [Permissions.RolesManage]));
        var before = new List<ActiveUserRoles> { new(Alice, ["Ops"]) };

        Assert.Equal([Permissions.RolesManage], RoleLockoutGuard.LostPermissions(before, matrix, [], matrix));
    }

    [Fact]
    public void Nothing_is_lost_if_nobody_had_the_permission_before()
    {
        var users = new List<ActiveUserRoles> { new(Alice, ["Viewer"]) };
        var matrix = Matrix(("Viewer", ["server.view"]));

        Assert.Empty(RoleLockoutGuard.LostPermissions(users, matrix, users, matrix));
    }

    [Fact]
    public void Non_super_admin_can_only_grant_own_permissions()
    {
        var own = new HashSet<string>(["server.view", "deployment.view"], StringComparer.Ordinal);

        Assert.Equal(["plugin.manage"], RoleLockoutGuard.NotGrantable(["server.view", "plugin.manage"], own, actorIsSuperAdmin: false));
        Assert.Empty(RoleLockoutGuard.NotGrantable(["plugin.manage"], own, actorIsSuperAdmin: true));
    }
}

public class PermissionCatalogTests
{
    [Fact]
    public void Catalog_lists_core_and_plugin_permissions_with_groups()
    {
        var provider = Substitute.For<IPermissionProvider>();
        provider.GetPermissions().Returns([new PermissionDefinition("sample.view", "Örnek görüntüleme")]);
        provider.GetDefaultRolePermissions().Returns(new Dictionary<string, IReadOnlyList<string>> { [Roles.Viewer] = ["sample.view"] });

        var catalog = new PermissionCatalog([provider]);

        Assert.True(catalog.IsKnown(Permissions.RolesManage));
        Assert.True(catalog.IsKnown("sample.view"));
        Assert.False(catalog.IsKnown("nope.view"));
        Assert.Equal("Kullanıcılar ve roller", catalog.All.Single(p => p.Name == Permissions.RolesManage).Group);
        Assert.True(catalog.All.Single(p => p.Name == "sample.view").IsPlugin);
        Assert.All(catalog.All.Where(p => !p.IsPlugin), p => Assert.NotEqual(PermissionGroups.Other, p.Group));
    }

    [Fact]
    public void Defaults_include_core_matrix_and_plugin_suggestions()
    {
        var provider = Substitute.For<IPermissionProvider>();
        provider.GetPermissions().Returns([new PermissionDefinition("sample.view", "Örnek")]);
        provider.GetDefaultRolePermissions().Returns(new Dictionary<string, IReadOnlyList<string>> { [Roles.Viewer] = ["sample.view"] });

        var catalog = new PermissionCatalog([provider]);

        Assert.Contains("sample.view", catalog.DefaultsFor(Roles.Viewer));
        Assert.Contains(Permissions.ServerView, catalog.DefaultsFor(Roles.Viewer));
        Assert.Contains(Permissions.RolesManage, catalog.DefaultsFor(Roles.Admin));
        Assert.DoesNotContain(Permissions.UserManage, catalog.DefaultsFor(Roles.Admin));
        Assert.Equal(catalog.All.Count, catalog.DefaultsFor(Roles.SuperAdmin).Count);
        Assert.Empty(catalog.DefaultsFor("Custom"));
    }
}
