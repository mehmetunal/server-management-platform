namespace ServerManager.Application.Auditing;

public static class AuditActions
{
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";
    public const string RecoveryCodeUsed = "auth.recovery_code_used";
    public const string PasswordChange = "account.password_change";
    public const string TwoFactorEnable = "account.2fa_enable";
    public const string TwoFactorDisable = "account.2fa_disable";
    public const string RecoveryCodesRegenerate = "account.recovery_codes_regenerate";

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

    public const string ProjectCreate = "project.create";
    public const string ProjectUpdate = "project.update";
    public const string ProjectDelete = "project.delete";
    public const string DeploymentStart = "deployment.start";
    public const string DeploymentComplete = "deployment.complete";
    public const string DeploymentCancel = "deployment.cancel";
    public const string DeploymentDomainCreate = "deployment_domain.create";
    public const string DeploymentDomainUpdate = "deployment_domain.update";
    public const string DeploymentDomainDelete = "deployment_domain.delete";
    public const string DeploymentProxyInstall = "deployment_proxy.install";

    // Proje ortam değişkenleri, webhook, geri dönüş ve yeniden başlatma
    public const string DeploymentRollback = "deployment.rollback";
    public const string DeploymentRestart = "deployment.restart";
    public const string ProjectEnvironmentUpdate = "project.env_update";
    public const string ProjectEnvironmentReveal = "project.env_reveal";
    public const string ProjectEnvironmentExport = "project.env_export";
    public const string ProjectWebhookUpdate = "project.webhook_update";
    public const string ProjectServiceLink = "project.service_link";
    public const string ProjectServiceUnlink = "project.service_unlink";

    public const string AlertRuleCreate = "alert_rule.create";
    public const string AlertRuleUpdate = "alert_rule.update";
    public const string AlertRuleDelete = "alert_rule.delete";
    public const string AlertAcknowledge = "alert.acknowledge";
    public const string NotificationChannelCreate = "notification_channel.create";
    public const string NotificationChannelUpdate = "notification_channel.update";
    public const string NotificationChannelDelete = "notification_channel.delete";
    public const string NotificationChannelTest = "notification_channel.test";
    public const string UptimeCheckCreate = "uptime_check.create";
    public const string UptimeCheckUpdate = "uptime_check.update";
    public const string UptimeCheckDelete = "uptime_check.delete";
    public const string SslMonitorCreate = "ssl_monitor.create";
    public const string SslMonitorUpdate = "ssl_monitor.update";
    public const string SslMonitorDelete = "ssl_monitor.delete";

    public const string BackupStorageCreate = "backup_storage.create";
    public const string BackupStorageUpdate = "backup_storage.update";
    public const string BackupStorageDelete = "backup_storage.delete";
    public const string BackupStorageTest = "backup_storage.test";
    public const string BackupJobCreate = "backup_job.create";
    public const string BackupJobUpdate = "backup_job.update";
    public const string BackupJobDelete = "backup_job.delete";
    public const string BackupStart = "backup.start";
    public const string BackupComplete = "backup.complete";
    public const string BackupCancel = "backup.cancel";
    public const string BackupRestore = "backup.restore";
    public const string BackupDownload = "backup.download";
    public const string BackupArtifactDelete = "backup.artifact_delete";

    public const string PluginInstall = "plugin.install";
    public const string PluginEnable = "plugin.enable";
    public const string PluginDisable = "plugin.disable";

    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
    public const string UserLock = "user.lock";
    public const string UserUnlock = "user.unlock";
    public const string UserTwoFactorReset = "user.2fa_reset";

    // Roller ve API anahtarları
    public const string RoleCreate = "role.create";
    public const string RoleUpdate = "role.update";
    public const string RoleDelete = "role.delete";
    public const string RolePermissionsChange = "role.permissions_change";
    public const string RoleAssign = "role.assign";
    public const string ApiKeyCreate = "api_key.create";
    public const string ApiKeyRevoke = "api_key.revoke";
    public const string ApiKeyUse = "api_key.use";

    public const string SecurityScan = "security.scan";
    public const string AuditExport = "audit.export";
    public const string AuditVerify = "audit.verify";
    public const string AuditChainAnchorCreate = "audit.chain_anchor_create";

    public const string SystemServiceControl = "system.service_control";
    public const string SystemProcessSignal = "system.process_signal";
    public const string ServerCleanup = "server.cleanup";

    public const string ServerGroupCreate = "server_group.create";
    public const string ServerGroupUpdate = "server_group.update";
    public const string ServerGroupDelete = "server_group.delete";

    public const string CommandRun = "command.run";
    public const string TemplateCreate = "template.create";
    public const string TemplateUpdate = "template.update";
    public const string TemplateDelete = "template.delete";

    public const string CloudAccountCreate = "cloud.account_create";
    public const string CloudAccountUpdate = "cloud.account_update";
    public const string CloudAccountDelete = "cloud.account_delete";
    public const string CloudSync = "cloud.sync";
    public const string CloudImport = "cloud.import";
    public const string CloudProvision = "cloud.provision";

    public const string AgentTokenCreate = "agent.token_create";
    public const string AgentTokenRevoke = "agent.token_revoke";

    public const string SettingsUpdate = "settings.update";

    // Servisler (tek tıkla Docker servisleri)
    public const string ManagedServiceCreate = "managed_service.create";
    public const string ManagedServiceRecreate = "managed_service.recreate";
    public const string ManagedServiceUpgrade = "managed_service.upgrade";
    public const string ManagedServiceRemove = "managed_service.remove";
    public const string ManagedServiceOperationComplete = "managed_service.operation_complete";
    public const string ManagedServiceRevealSecrets = "managed_service.reveal_secrets";
    public const string ManagedServiceConsoleOpen = "managed_service.console_open";
    public const string ManagedServiceConsoleClose = "managed_service.console_close";
    public const string ManagedServiceContainerAction = "managed_service.container_action";
    public const string ManagedServiceFirewallApply = "managed_service.firewall_apply";

    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        [Login] = "Giriş",
        [LoginFailed] = "Başarısız giriş",
        [Logout] = "Çıkış",
        [RecoveryCodeUsed] = "Kurtarma koduyla giriş",
        [PasswordChange] = "Parola değiştirme",
        [TwoFactorEnable] = "İki adımlı doğrulama açıldı",
        [TwoFactorDisable] = "İki adımlı doğrulama kapatıldı",
        [RecoveryCodesRegenerate] = "Kurtarma kodları yenilendi",
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
        [ProjectCreate] = "Proje ekleme",
        [ProjectUpdate] = "Proje güncelleme",
        [ProjectDelete] = "Proje silme",
        [DeploymentStart] = "Deployment başlatıldı",
        [DeploymentComplete] = "Deployment tamamlandı",
        [DeploymentCancel] = "Deployment iptal edildi",
        [DeploymentDomainCreate] = "Domain ekleme",
        [DeploymentDomainUpdate] = "Domain güncelleme",
        [DeploymentDomainDelete] = "Domain silme",
        [DeploymentProxyInstall] = "Vekil kurulumu",
        [DeploymentRollback] = "Önceki sürüme geri dönüş başlatıldı",
        [DeploymentRestart] = "Build olmadan yeniden başlatma başlatıldı",
        [ProjectEnvironmentUpdate] = "Proje ortam değişkenleri güncellendi",
        [ProjectEnvironmentReveal] = "Proje ortam değişkeni değeri görüntülendi",
        [ProjectEnvironmentExport] = "Proje ortam değişkenleri (.env) indirildi",
        [ProjectWebhookUpdate] = "Proje push webhook ayarı güncellendi",
        [ProjectServiceLink] = "Servis projeye bağlandı",
        [ProjectServiceUnlink] = "Servisin proje bağı kaldırıldı",
        [AlertRuleCreate] = "Alarm kuralı ekleme",
        [AlertRuleUpdate] = "Alarm kuralı güncelleme",
        [AlertRuleDelete] = "Alarm kuralı silme",
        [AlertAcknowledge] = "Alarm üstlenildi",
        [NotificationChannelCreate] = "Bildirim kanalı ekleme",
        [NotificationChannelUpdate] = "Bildirim kanalı güncelleme",
        [NotificationChannelDelete] = "Bildirim kanalı silme",
        [NotificationChannelTest] = "Test bildirimi",
        [UptimeCheckCreate] = "Uptime kontrolü ekleme",
        [UptimeCheckUpdate] = "Uptime kontrolü güncelleme",
        [UptimeCheckDelete] = "Uptime kontrolü silme",
        [SslMonitorCreate] = "SSL izleme ekleme",
        [SslMonitorUpdate] = "SSL izleme güncelleme",
        [SslMonitorDelete] = "SSL izleme silme",
        [BackupStorageCreate] = "Yedek depolama ekleme",
        [BackupStorageUpdate] = "Yedek depolama güncelleme",
        [BackupStorageDelete] = "Yedek depolama silme",
        [BackupStorageTest] = "Yedek depolama testi",
        [BackupJobCreate] = "Yedekleme işi ekleme",
        [BackupJobUpdate] = "Yedekleme işi güncelleme",
        [BackupJobDelete] = "Yedekleme işi silme",
        [BackupStart] = "Yedekleme başlatıldı",
        [BackupComplete] = "Yedekleme tamamlandı",
        [BackupCancel] = "Yedekleme iptal edildi",
        [BackupRestore] = "Yedek geri yükleme",
        [BackupDownload] = "Yedek indirme",
        [BackupArtifactDelete] = "Yedek dosyası silme",
        [PluginInstall] = "Eklenti kurulumu",
        [PluginEnable] = "Eklenti etkinleştirildi",
        [PluginDisable] = "Eklenti devre dışı bırakıldı",
        [UserCreate] = "Kullanıcı ekleme",
        [UserUpdate] = "Kullanıcı güncelleme",
        [UserLock] = "Kullanıcı kilitleme",
        [UserUnlock] = "Kullanıcı kilidi açma",
        [UserTwoFactorReset] = "Kullanıcının iki adımlı doğrulaması sıfırlandı",
        [SecurityScan] = "Güvenlik taraması",
        [AuditExport] = "Audit log dışa aktarma",
        [AuditVerify] = "Audit log bütünlük doğrulaması",
        [AuditChainAnchorCreate] = "Audit zincir çapası oluşturuldu",
        [SystemServiceControl] = "Sunucu servisi başlatma / durdurma / yeniden başlatma",
        [SystemProcessSignal] = "Process sonlandırma",
        [ServerCleanup] = "Sunucu temizliği",
        [ServerGroupCreate] = "Sunucu grubu ekleme",
        [ServerGroupUpdate] = "Sunucu grubu güncelleme",
        [ServerGroupDelete] = "Sunucu grubu silme",
        [CommandRun] = "Toplu komut çalıştırma",
        [TemplateCreate] = "Sunucu şablonu ekleme",
        [TemplateUpdate] = "Sunucu şablonu güncelleme",
        [TemplateDelete] = "Sunucu şablonu silme",
        [CloudAccountCreate] = "Bulut hesabı ekleme",
        [CloudAccountUpdate] = "Bulut hesabı güncelleme",
        [CloudAccountDelete] = "Bulut hesabı silme",
        [CloudSync] = "Bulut sunucu listesini eşitleme",
        [CloudImport] = "Bulut sunucusunu içe aktarma",
        [CloudProvision] = "Bulutta sunucu oluşturma",
        [AgentTokenCreate] = "Agent anahtarı oluşturma",
        [AgentTokenRevoke] = "Agent anahtarı iptali",
        [SettingsUpdate] = "Sistem ayarı güncelleme",

        // Servisler
        [ManagedServiceCreate] = "Servis kurulumu",
        [ManagedServiceRecreate] = "Servis yeniden oluşturma",
        [ManagedServiceUpgrade] = "Servis sürüm yükseltme",
        [ManagedServiceRemove] = "Servis kaldırma",
        [ManagedServiceOperationComplete] = "Servis işlemi tamamlandı",
        [ManagedServiceRevealSecrets] = "Servis parolası görüntülendi",
        [ManagedServiceConsoleOpen] = "Servis konsolu açıldı",
        [ManagedServiceConsoleClose] = "Servis konsolu kapandı",
        [ManagedServiceContainerAction] = "Servis başlatma / durdurma",
        [ManagedServiceFirewallApply] = "Servis güvenlik duvarı kuralları",

        // Roller ve API anahtarları
        [RoleCreate] = "Rol ekleme",
        [RoleUpdate] = "Rol güncelleme",
        [RoleDelete] = "Rol silme",
        [RolePermissionsChange] = "Rol izinlerini değiştirme",
        [RoleAssign] = "Kullanıcıya rol atama",
        [ApiKeyCreate] = "API anahtarı oluşturma",
        [ApiKeyRevoke] = "API anahtarı iptali",
        [ApiKeyUse] = "API anahtarı kullanımı"
    };
}
