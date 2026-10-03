namespace ServerManager.Plugin.DevOps.Dokploy.Core;

public sealed class DokployOptions
{
    public const string SectionName = "Dokploy";

    /// <summary>Resmi kurulum betiği. Sunucuya indirilir, SHA-256 özeti kurulum kaydına yazılır ve root olarak çalıştırılır.</summary>
    public string InstallScriptUrl { get; set; } = "https://dokploy.com/install.sh";

    /// <summary>İnternet kontrolünde betik adresine ek olarak erişilmesi gereken Docker registry.</summary>
    public string RegistryCheckUrl { get; set; } = "https://registry-1.docker.io/v2/";

    public int Port { get; set; } = 3000;

    public List<int> RequiredPorts { get; set; } = [80, 443, 3000];

    public int MinMemoryMb { get; set; } = 2048;

    /// <summary>Altındaki boş disk uyarı olarak gösterilir; kurulumu engellemez.</summary>
    public int RecommendedDiskGb { get; set; } = 30;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public int InstallTimeoutMinutes { get; set; } = 30;

    /// <summary>Kurulumdan sonra Dokploy'un /api/health yanıtı için beklenen en uzun süre.</summary>
    public int StartupTimeoutSeconds { get; set; } = 180;

    public int HttpTimeoutSeconds { get; set; } = 10;

    /// <summary>0 ise periyodik sağlık kontrolü yapılmaz.</summary>
    public int HealthCheckIntervalMinutes { get; set; } = 5;

    /// <summary>Kurulum kaydında saklanan çıktının üst sınırı (en sondaki kısım tutulur).</summary>
    public int MaxStoredOutputKilobytes { get; set; } = 512;

    public int InstallationHistoryCount { get; set; } = 10;
}
