namespace ServerManager.Application.Authorization;

public static class DefaultRolePermissions
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Matrix = new Dictionary<string, IReadOnlyList<string>>
    {
        [Roles.SuperAdmin] = Permissions.All,
        [Roles.Admin] =
        [
            Permissions.DashboardView,
            Permissions.ServerView,
            Permissions.ServerCreate,
            Permissions.ServerEdit,
            Permissions.ServerDelete,
            Permissions.ServerConnect,
            Permissions.AuditView
        ],
        [Roles.Operator] =
        [
            Permissions.DashboardView,
            Permissions.ServerView,
            Permissions.ServerConnect
        ],
        [Roles.Developer] =
        [
            Permissions.DashboardView,
            Permissions.ServerView
        ],
        [Roles.Viewer] =
        [
            Permissions.DashboardView,
            Permissions.ServerView
        ]
    };
}
