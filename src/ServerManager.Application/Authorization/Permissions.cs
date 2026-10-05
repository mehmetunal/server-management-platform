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

    public const string DeploymentView = "deployment.view";
    public const string DeploymentManage = "deployment.manage";
    public const string DeploymentExecute = "deployment.execute";

    /// <summary>Proje ortam değişkeni değerlerini görme ve .env olarak dışa aktarma (yazma DeploymentManage ile).</summary>
    public const string DeploymentSecrets = "deployment.secrets";

    public const string AlertView = "alert.view";
    public const string AlertAcknowledge = "alert.acknowledge";
    public const string AlertManage = "alert.manage";

    public const string BackupView = "backup.view";
    public const string BackupExecute = "backup.execute";
    public const string BackupManage = "backup.manage";
    public const string BackupRestore = "backup.restore";

    public const string PluginManage = "plugin.manage";

    public const string UserManage = "user.manage";

    public const string AuditView = "audit.view";
    public const string AuditExport = "audit.export";

    public const string SecurityView = "security.view";
    public const string SecurityScan = "security.scan";

    public const string SystemView = "system.view";
    public const string SystemManage = "system.manage";

    public const string CommandView = "command.view";
    public const string CommandRun = "command.run";
    public const string TemplateManage = "template.manage";

    public const string CloudView = "cloud.view";
    public const string CloudManage = "cloud.manage";
    public const string CloudProvision = "cloud.provision";

    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";

    // Servisler (tek tıkla Docker servisleri)
    public const string ServicesView = "services.view";
    public const string ServicesManage = "services.manage";
    public const string ServicesConsole = "services.console";
    public const string ServicesRevealSecrets = "services.reveal_secrets";

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
        DeploymentView,
        DeploymentManage,
        DeploymentExecute,
        DeploymentSecrets,
        AlertView,
        AlertAcknowledge,
        AlertManage,
        BackupView,
        BackupExecute,
        BackupManage,
        BackupRestore,
        PluginManage,
        UserManage,
        AuditView,
        AuditExport,
        SecurityView,
        SecurityScan,
        SystemView,
        SystemManage,
        CommandView,
        CommandRun,
        TemplateManage,
        CloudView,
        CloudManage,
        CloudProvision,
        SettingsView,
        SettingsManage,
        ServicesView,
        ServicesManage,
        ServicesConsole,
        ServicesRevealSecrets
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
        [DeploymentView] = "Deployment projeleri, geçmişi ve logları görüntüleme",
        [DeploymentManage] = "Deployment projesi ekleme, düzenleme, silme (Git erişim anahtarı ve ortam değişkenleri dahil)",
        [DeploymentExecute] = "Deployment başlatma, iptal etme, yeniden dağıtma, geri dönüş ve yeniden başlatma",
        [DeploymentSecrets] = "Proje ortam değişkeni değerlerini görme ve .env olarak indirme",
        [AlertView] = "Alarmlar, uptime kontrolleri ve SSL sertifikalarını görüntüleme",
        [AlertAcknowledge] = "Alarmı üstlenme (görüldü olarak işaretleme)",
        [AlertManage] = "Alarm kuralları, bildirim kanalları, uptime ve SSL kontrollerini yönetme",
        [BackupView] = "Yedekleme işleri, depolama hedefleri ve yedek geçmişini görüntüleme",
        [BackupExecute] = "Yedeklemeyi elle başlatma ve süren yedeklemeyi iptal etme",
        [BackupManage] = "Yedekleme işi ve depolama hedefi ekleme, düzenleme, silme (veritabanı parolası ve şifreleme parolası dahil); yedek dosyası silme",
        [BackupRestore] = "Yedeği geri yükleme ve şifresi çözülmüş yedeği indirme",
        [PluginManage] = "Eklenti kurma, etkinleştirme ve devre dışı bırakma",
        [UserManage] = "Kullanıcı yönetimi",
        [AuditView] = "Audit log görüntüleme",
        [AuditExport] = "Audit log'u CSV olarak dışa aktarma ve bütünlüğünü doğrulama",
        [SecurityView] = "Güvenlik merkezi ve sunucu güvenlik taramalarını görüntüleme",
        [SecurityScan] = "Sunucuda güvenlik taraması başlatma (yalnızca okuma yapar)",
        [SystemView] = "Sunucu servisleri, process'ler, loglar, ağ ve disk bilgisini görüntüleme",
        [SystemManage] = "Sunucu servisini başlatma, durdurma, yeniden başlatma ve process sonlandırma",
        [CommandView] = "Toplu komut geçmişini ve sunucu şablonlarını görüntüleme",
        [CommandRun] = "Birden fazla sunucuda aynı anda komut çalıştırma",
        [TemplateManage] = "Sunucu şablonlarını (betik / cloud-init) ekleme, düzenleme ve silme",
        [CloudView] = "Bulut sağlayıcı hesaplarını, sunucu listesini ve maliyetleri görüntüleme",
        [CloudManage] = "Bulut sağlayıcı hesabı ekleme, düzenleme, silme ve eşitleme",
        [CloudProvision] = "Bulut sağlayıcıda yeni sunucu oluşturma",
        [SettingsView] = "Sistem ayarlarını ve uygulama bilgisini görüntüleme",
        [SettingsManage] = "İzleme, alarm, yedekleme, tarama ve bulut aralıklarını değiştirme",
        [ServicesView] = "Servisleri (veritabanı ve uygulamalar), loglarını ve işlem geçmişini görüntüleme",
        [ServicesManage] = "Servis kurma, ayarlarını değiştirip yeniden oluşturma, sürüm yükseltme, durdurma ve kaldırma",
        [ServicesConsole] = "Servis konsolunu açma (psql, mysql, redis-cli …)",
        [ServicesRevealSecrets] = "Servis parolalarını ve parolalı bağlantı adreslerini görüntüleme"
    };
}
