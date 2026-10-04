namespace ServerManager.Application.Auditing;

public static class AuditEntityTypes
{
    public const string Server = "Server";
    public const string User = "User";
    public const string Plugin = "Plugin";
    public const string Project = "Project";
    public const string AlertRule = "AlertRule";
    public const string AlertEvent = "AlertEvent";
    public const string NotificationChannel = "NotificationChannel";
    public const string UptimeCheck = "UptimeCheck";
    public const string SslMonitor = "SslMonitor";
    public const string BackupStorage = "BackupStorage";
    public const string BackupJob = "BackupJob";
    public const string BackupRun = "BackupRun";
    public const string AuditLog = "AuditLog";

    public static IReadOnlyDictionary<string, string> DisplayNames { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Server] = "Sunucu",
        [User] = "Kullanıcı",
        [Plugin] = "Eklenti",
        [Project] = "Proje",
        [AlertRule] = "Alarm kuralı",
        [AlertEvent] = "Alarm",
        [NotificationChannel] = "Bildirim kanalı",
        [UptimeCheck] = "Uptime kontrolü",
        [SslMonitor] = "SSL izleme",
        [BackupStorage] = "Yedek deposu",
        [BackupJob] = "Yedekleme görevi",
        [BackupRun] = "Yedekleme çalıştırması",
        [AuditLog] = "Audit log"
    };

    public static string DisplayName(string? entityType) =>
        entityType is null ? "—" : DisplayNames.GetValueOrDefault(entityType, entityType);
}
