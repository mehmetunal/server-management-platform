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

    public const string DockerView = "docker.view";
    public const string DockerStart = "docker.start";
    public const string DockerStop = "docker.stop";
    public const string DockerRestart = "docker.restart";
    public const string DockerDelete = "docker.delete";
    public const string DockerManage = "docker.manage";
    public const string DockerTerminal = "docker.terminal";

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
        DockerView,
        DockerStart,
        DockerStop,
        DockerRestart,
        DockerDelete,
        DockerManage,
        DockerTerminal,
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
        [DockerView] = "Docker görüntüleme (container, image, volume, network, log)",
        [DockerStart] = "Container başlatma / devam ettirme",
        [DockerStop] = "Container durdurma / duraklatma / kill",
        [DockerRestart] = "Container yeniden başlatma",
        [DockerDelete] = "Docker silme (container, image, volume, network, prune)",
        [DockerManage] = "Docker yönetimi (image pull, volume/network oluşturma, yeniden adlandırma)",
        [DockerTerminal] = "Container terminali",
        [UserManage] = "Kullanıcı yönetimi",
        [AuditView] = "Audit log görüntüleme"
    };
}
