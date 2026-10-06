using System.Globalization;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

public static class AlertRuleKinds
{
    public const int MaxDurationMinutes = 1440;
    public const int MaxSslThresholdDays = 365;
    public const int MaxRestartThreshold = 1000;
    public const int MaxReclaimableGigabytes = 100_000;
    public const long BytesPerGigabyte = 1024L * 1024 * 1024;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static bool IsMetric(AlertRuleKind kind) =>
        kind is AlertRuleKind.CpuUsage or AlertRuleKind.MemoryUsage or AlertRuleKind.DiskUsage;

    public static bool UsesThreshold(AlertRuleKind kind) =>
        IsMetric(kind) || kind is AlertRuleKind.SslCertificateExpiry or AlertRuleKind.ContainerRestartLoop or AlertRuleKind.ReclaimableSpace;

    public static bool UsesDuration(AlertRuleKind kind) =>
        IsMetric(kind) || kind is AlertRuleKind.ServerOffline or AlertRuleKind.UptimeCheckDown
            or AlertRuleKind.ServiceDown or AlertRuleKind.ContainerRestartLoop;

    /// <summary>Tek bir yönetilen servise daraltılabilen kurallar.</summary>
    public static bool UsesService(AlertRuleKind kind) => kind is AlertRuleKind.ServiceDown or AlertRuleKind.ContainerRestartLoop;

    /// <summary>Kaynak geçmişi toplayıcısının container örneklerine dayanan kurallar.</summary>
    public static bool UsesContainerSamples(AlertRuleKind kind) => UsesService(kind);

    /// <summary>Form eşik birimi: percent, days, count, gb veya none.</summary>
    public static string ThresholdUnit(AlertRuleKind kind) => kind switch
    {
        _ when IsMetric(kind) => "percent",
        AlertRuleKind.SslCertificateExpiry => "days",
        AlertRuleKind.ContainerRestartLoop => "count",
        AlertRuleKind.ReclaimableSpace => "gb",
        _ => "none"
    };

    /// <summary>Yeni kural formunda türe göre önerilen eşik ve süre.</summary>
    public static (double Threshold, int DurationMinutes) Defaults(AlertRuleKind kind) => kind switch
    {
        AlertRuleKind.SslCertificateExpiry => (14, 0),
        AlertRuleKind.ServiceDown => (0, 5),
        AlertRuleKind.ContainerRestartLoop => (3, 30),
        AlertRuleKind.ReclaimableSpace => (10, 0),
        AlertRuleKind.ServerOffline or AlertRuleKind.UptimeCheckDown => (0, 5),
        _ when IsMetric(kind) => (90, 5),
        _ => (0, 0)
    };

    /// <summary>Yeniden başlama döngüsü alarmının hedef anahtarı (sunucu + container adı).</summary>
    public static string ContainerTargetKey(Guid serverId, string containerName) => $"{serverId:N}:{containerName}";

    public static string Gigabytes(long bytes) => (bytes / (double)BytesPerGigabyte).ToString("0.#", Turkish) + " GB";

    public static string DisplayName(AlertRuleKind kind) => kind switch
    {
        AlertRuleKind.CpuUsage => "CPU kullanımı",
        AlertRuleKind.MemoryUsage => "RAM kullanımı",
        AlertRuleKind.DiskUsage => "Disk kullanımı",
        AlertRuleKind.ServerOffline => "Sunucu erişilemiyor",
        AlertRuleKind.UptimeCheckDown => "Uptime kontrolü başarısız",
        AlertRuleKind.SslCertificateExpiry => "SSL sertifikası bitiyor",
        AlertRuleKind.DeploymentFailed => "Deployment başarısız",
        AlertRuleKind.BackupFailed => "Yedekleme başarısız",
        AlertRuleKind.SecurityFinding => "Kritik güvenlik bulgusu",
        AlertRuleKind.ServiceDown => "Servis çalışmıyor",
        AlertRuleKind.ContainerRestartLoop => "Container yeniden başlama döngüsü",
        AlertRuleKind.ReclaimableSpace => "Temizlenebilir alan",
        _ => kind.ToString()
    };

    public static string Describe(AlertRuleKind kind, double threshold, int durationMinutes)
    {
        var duration = UsesDuration(kind) && durationMinutes > 0 ? $", {durationMinutes} dk boyunca" : string.Empty;
        return kind switch
        {
            AlertRuleKind.CpuUsage or AlertRuleKind.MemoryUsage or AlertRuleKind.DiskUsage =>
                $"{DisplayName(kind)} > {Percent(threshold)}{duration}",
            AlertRuleKind.SslCertificateExpiry => $"Bitişe {Number(threshold)} günden az",
            AlertRuleKind.ServerOffline or AlertRuleKind.UptimeCheckDown =>
                durationMinutes > 0 ? $"{durationMinutes} dk boyunca" : "İlk hatada",
            AlertRuleKind.BackupFailed => "Son yedekleme başarısız olduğunda",
            AlertRuleKind.SecurityFinding => "Son güvenlik taramasında kritik bulgu olduğunda",
            AlertRuleKind.ServiceDown => durationMinutes > 0
                ? $"Container {durationMinutes} dk boyunca çalışmıyor veya sağlıksız"
                : "Container çalışmıyor veya sağlıksız",
            AlertRuleKind.ContainerRestartLoop => $"{durationMinutes} dk içinde ≥ {Number(threshold)} yeniden başlama",
            AlertRuleKind.ReclaimableSpace => $"Temizlenebilir alan ≥ {Number(threshold)} GB",
            _ => "Son deployment başarısız olduğunda"
        };
    }

    public static string Percent(double value) => "%" + value.ToString("0.#", Turkish);

    public static string Number(double value) => value.ToString("0.#", Turkish);

    public static string Date(DateTime value) => value.ToString("dd.MM.yyyy", Turkish);
}
