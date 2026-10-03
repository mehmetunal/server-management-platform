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

    public const string TerminalView = "terminal.view";
    public const string TerminalExecute = "terminal.execute";

    public const string FileView = "file.view";
    public const string FileCreate = "file.create";
    public const string FileEdit = "file.edit";
    public const string FileDelete = "file.delete";
    public const string FileUpload = "file.upload";
    public const string FileDownload = "file.download";
    public const string FilePermissions = "file.permissions";

    public const string DokployView = "dokploy.view";
    public const string DokployInstall = "dokploy.install";
    public const string DokployManage = "dokploy.manage";

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
        TerminalView,
        TerminalExecute,
        FileView,
        FileCreate,
        FileEdit,
        FileDelete,
        FileUpload,
        FileDownload,
        FilePermissions,
        DokployView,
        DokployInstall,
        DokployManage,
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
        [TerminalView] = "Terminal sayfası ve oturum geçmişi görüntüleme",
        [TerminalExecute] = "Sunucuda terminal oturumu açma",
        [FileView] = "Dosya listeleme ve içerik görüntüleme",
        [FileCreate] = "Dosya / klasör oluşturma ve kopyalama",
        [FileEdit] = "Dosya düzenleme, yeniden adlandırma ve taşıma",
        [FileDelete] = "Dosya / klasör silme",
        [FileUpload] = "Dosya yükleme",
        [FileDownload] = "Dosya indirme",
        [FilePermissions] = "Dosya izinleri ve sahiplik değiştirme (chmod / chown)",
        [DokployView] = "Dokploy durumu, projeler ve kurulum geçmişi görüntüleme",
        [DokployInstall] = "Sunucuya Dokploy kurma",
        [DokployManage] = "Dokploy bağlantı ayarları ve API anahtarı yönetimi",
        [UserManage] = "Kullanıcı yönetimi",
        [AuditView] = "Audit log görüntüleme"
    };
}
