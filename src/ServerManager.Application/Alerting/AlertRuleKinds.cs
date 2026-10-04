using System.Globalization;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

public static class AlertRuleKinds
{
    public const int MaxDurationMinutes = 1440;
    public const int MaxSslThresholdDays = 365;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static bool IsMetric(AlertRuleKind kind) =>
        kind is AlertRuleKind.CpuUsage or AlertRuleKind.MemoryUsage or AlertRuleKind.DiskUsage;

    public static bool UsesThreshold(AlertRuleKind kind) => IsMetric(kind) || kind == AlertRuleKind.SslCertificateExpiry;

    public static bool UsesDuration(AlertRuleKind kind) =>
        IsMetric(kind) || kind is AlertRuleKind.ServerOffline or AlertRuleKind.UptimeCheckDown;

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
            _ => "Son deployment başarısız olduğunda"
        };
    }

    public static string Percent(double value) => "%" + value.ToString("0.#", Turkish);

    public static string Number(double value) => value.ToString("0.#", Turkish);

    public static string Date(DateTime value) => value.ToString("dd.MM.yyyy", Turkish);
}
