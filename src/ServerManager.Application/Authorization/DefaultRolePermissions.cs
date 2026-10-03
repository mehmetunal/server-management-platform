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
            Permissions.TerminalView,
            Permissions.TerminalExecute,
            Permissions.FileView,
            Permissions.FileCreate,
            Permissions.FileEdit,
            Permissions.FileDelete,
            Permissions.FileUpload,
            Permissions.FileDownload,
            Permissions.FilePermissions,
            Permissions.DeploymentView,
            Permissions.DeploymentManage,
            Permissions.DeploymentExecute,
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
            Permissions.DockerTerminal,
            Permissions.TerminalView,
            Permissions.TerminalExecute,
            Permissions.FileView,
            Permissions.FileCreate,
            Permissions.FileEdit,
            Permissions.FileUpload,
            Permissions.FileDownload,
            Permissions.DeploymentView,
            Permissions.DeploymentExecute
        ],
        [Roles.Developer] =
        [
            Permissions.DashboardView,
            Permissions.ServerView,
            Permissions.DockerView,
            Permissions.DockerRestart,
            Permissions.FileView,
            Permissions.FileDownload,
            Permissions.DeploymentView,
            Permissions.DeploymentExecute
        ],
        [Roles.Viewer] =
        [
            Permissions.DashboardView,
            Permissions.ServerView,
            Permissions.DockerView,
            Permissions.DeploymentView
        ]
    };
}
