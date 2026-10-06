namespace ServerManager.Web.Helpers;

/// <summary>
/// Sayfa başlıklarındaki "?" yardım bağlantılarının kılavuzda gittiği bölümler. Değerler docs/kullanim-kilavuzu.md
/// içindeki <c>{#id}</c> başlık kimlikleridir; bir başlığın kimliğini değiştirirseniz burayı da güncelleyin.
/// Testler, buradaki her kimliğin işlenmiş kılavuzda bulunduğunu doğrular.
/// </summary>
public static class GuideAnchors
{
    public const string Session = "oturum";
    public const string TwoFactor = "iki-adimli-dogrulama";
    public const string Servers = "sunucular";
    public const string ServerPage = "sunucu-sayfasi";
    public const string Monitoring = "izleme";
    public const string Agent = "agent-kurulumu";
    public const string Uptime = "uptime-ve-ssl";
    public const string Alerts = "alarmlar";
    public const string Docker = "docker";
    public const string Terminal = "terminal";
    public const string Files = "dosya-yoneticisi";
    public const string System = "sistem-servisleri";
    public const string Cleanup = "temizlik";
    public const string ResourceUsage = "kaynak-kullanimi";
    public const string Backups = "yedekleme";
    public const string BackupDownload = "yedek-indirme";
    public const string Projects = "projeler";
    public const string Environment = "ortam-degiskenleri";
    public const string Webhook = "otomatik-deploy";
    public const string Domains = "domainler";
    public const string Services = "servisler";
    public const string Commands = "toplu-komut";
    public const string Cloud = "bulut";
    public const string Security = "guvenlik";
    public const string Users = "kullanicilar";
    public const string Roles = "roller";
    public const string ApiKeys = "api-anahtarlari";
    public const string AuditLog = "denetim-kaydi";
    public const string Settings = "ayarlar";
    public const string Plugins = "eklentiler";
    public const string Troubleshooting = "sorun-giderme";

    public static IReadOnlyList<string> All { get; } =
    [
        Session, TwoFactor, Servers, ServerPage, Monitoring, Agent, Uptime, Alerts, Docker, Terminal, Files, System,
        Cleanup, ResourceUsage, Backups, BackupDownload, Projects, Environment, Webhook, Domains, Services, Commands,
        Cloud, Security, Users, Roles, ApiKeys, AuditLog, Settings, Plugins, Troubleshooting
    ];

    /// <summary>Sunucu seçme sayfasının (Docker, Terminal, Dosyalar …) kılavuzdaki bölümü.</summary>
    public static string ForPicker(string key) => key switch
    {
        "docker" or "images" or "volumes" or "networks" => Docker,
        "terminal" => Terminal,
        "files" => Files,
        "services" or "processes" or "logs" => System,
        "metrics" => Monitoring,
        _ => Servers
    };
}
