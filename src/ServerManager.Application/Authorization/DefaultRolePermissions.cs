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
            Permissions.DockerView,
            Permissions.DockerStart,
            Permissions.DockerStop,
            Permissions.DockerRestart,
            Permissions.DockerDelete,
            Permissions.DockerManage,
            Permissions.DockerTerminal,
            Permissions.AuditView
        ],
        [Roles.Operator] =
        [
            Permissions.DashboardView,
            Permissions.ServerView,
            Permissions.ServerConnect,
            Permissions.DockerView,
            Permissions.DockerStart,
            Permissions.DockerStop,
            Permissions.DockerRestart,
            Permissions.DockerTerminal
        ],
        [Roles.Developer] =
        [
            Permissions.DashboardView,
            Permissions.ServerView,
            Permissions.DockerView,
            Permissions.DockerRestart
        ],
        [Roles.Viewer] =
        [
            Permissions.DashboardView,
            Permissions.ServerView,
            Permissions.DockerView
        ]
    };
}
