namespace ServerManager.Application.Authorization;

public static class Permissions
{
    public const string ClaimType = "permission";

    public const string DashboardView = "dashboard.view";

    public const string ServerView = "server.view";
    public const string ServerCreate = "server.create";
    public const string ServerEdit = "server.edit";
    public const string ServerDelete = "server.delete";
    public const string ServerConnect = "server.connect";

    public const string UserManage = "user.manage";

    public const string AuditView = "audit.view";

    public static readonly IReadOnlyList<string> All =
    [
        DashboardView,
        ServerView,
        ServerCreate,
        ServerEdit,
        ServerDelete,
        ServerConnect,
        UserManage,
        AuditView
    ];

    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        [DashboardView] = "Dashboard görüntüleme",
        [ServerView] = "Sunucu görüntüleme",
        [ServerCreate] = "Sunucu ekleme",
        [ServerEdit] = "Sunucu düzenleme",
        [ServerDelete] = "Sunucu silme",
        [ServerConnect] = "Sunucuya bağlanma",
        [UserManage] = "Kullanıcı yönetimi",
        [AuditView] = "Audit log görüntüleme"
    };
}
