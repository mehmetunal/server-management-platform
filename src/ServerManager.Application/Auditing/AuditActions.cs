namespace ServerManager.Application.Auditing;

public static class AuditActions
{
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";

    public const string ServerCreate = "server.create";
    public const string ServerUpdate = "server.update";
    public const string ServerDelete = "server.delete";
    public const string ServerConnectionTest = "server.connection_test";
    public const string ServerStatusChanged = "server.status_changed";
    public const string ServerMetricsCollect = "server.metrics_collect";

    public const string DockerContainerStart = "docker.container_start";
    public const string DockerContainerStop = "docker.container_stop";
    public const string DockerContainerRestart = "docker.container_restart";
    public const string DockerContainerPause = "docker.container_pause";
    public const string DockerContainerUnpause = "docker.container_unpause";
    public const string DockerContainerKill = "docker.container_kill";
    public const string DockerContainerRemove = "docker.container_remove";
    public const string DockerContainerRename = "docker.container_rename";
    public const string DockerImagePull = "docker.image_pull";
    public const string DockerImageRemove = "docker.image_remove";
    public const string DockerImagePrune = "docker.image_prune";
    public const string DockerVolumeCreate = "docker.volume_create";
    public const string DockerVolumeRemove = "docker.volume_remove";
    public const string DockerNetworkCreate = "docker.network_create";
    public const string DockerNetworkRemove = "docker.network_remove";
    public const string DockerTerminalOpen = "docker.terminal_open";
    public const string DockerTerminalClose = "docker.terminal_close";

    public const string TerminalOpen = "terminal.open";
    public const string TerminalClose = "terminal.close";
    public const string TerminalDangerousCommand = "terminal.dangerous_command";

    public const string FileRead = "file.read";
    public const string FileDownload = "file.download";
    public const string FileCreate = "file.create";
    public const string FileDirectoryCreate = "file.directory_create";
    public const string FileEdit = "file.edit";
    public const string FileUpload = "file.upload";
    public const string FileDelete = "file.delete";
    public const string FileMove = "file.move";
    public const string FileCopy = "file.copy";
    public const string FilePermissions = "file.permissions";

    public const string PluginInstall = "plugin.install";
    public const string PluginEnable = "plugin.enable";
    public const string PluginDisable = "plugin.disable";

    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
    public const string UserLock = "user.lock";
    public const string UserUnlock = "user.unlock";

    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        [Login] = "Giriş",
        [LoginFailed] = "Başarısız giriş",
        [Logout] = "Çıkış",
        [ServerCreate] = "Sunucu ekleme",
        [ServerUpdate] = "Sunucu güncelleme",
        [ServerDelete] = "Sunucu silme",
        [ServerConnectionTest] = "Bağlantı testi",
        [ServerStatusChanged] = "Sunucu durumu değişti",
        [ServerMetricsCollect] = "Metrik toplama",
        [DockerContainerStart] = "Container başlatma",
        [DockerContainerStop] = "Container durdurma",
        [DockerContainerRestart] = "Container yeniden başlatma",
        [DockerContainerPause] = "Container duraklatma",
        [DockerContainerUnpause] = "Container devam ettirme",
        [DockerContainerKill] = "Container kill",
        [DockerContainerRemove] = "Container silme",
        [DockerContainerRename] = "Container yeniden adlandırma",
        [DockerImagePull] = "Image pull",
        [DockerImageRemove] = "Image silme",
        [DockerImagePrune] = "Image prune",
        [DockerVolumeCreate] = "Volume oluşturma",
        [DockerVolumeRemove] = "Volume silme",
        [DockerNetworkCreate] = "Network oluşturma",
        [DockerNetworkRemove] = "Network silme",
        [DockerTerminalOpen] = "Container terminali açıldı",
        [DockerTerminalClose] = "Container terminali kapandı",
        [TerminalOpen] = "Terminal açıldı",
        [TerminalClose] = "Terminal kapandı",
        [TerminalDangerousCommand] = "Tehlikeli komut",
        [FileRead] = "Dosya görüntüleme",
        [FileDownload] = "Dosya indirme",
        [FileCreate] = "Dosya oluşturma",
        [FileDirectoryCreate] = "Klasör oluşturma",
        [FileEdit] = "Dosya düzenleme",
        [FileUpload] = "Dosya yükleme",
        [FileDelete] = "Dosya / klasör silme",
        [FileMove] = "Dosya taşıma / yeniden adlandırma",
        [FileCopy] = "Dosya kopyalama",
        [FilePermissions] = "Dosya izinleri değiştirme",
        [PluginInstall] = "Eklenti kurulumu",
        [PluginEnable] = "Eklenti etkinleştirildi",
        [PluginDisable] = "Eklenti devre dışı bırakıldı",
        [UserCreate] = "Kullanıcı ekleme",
        [UserUpdate] = "Kullanıcı güncelleme",
        [UserLock] = "Kullanıcı kilitleme",
        [UserUnlock] = "Kullanıcı kilidi açma"
    };
}
