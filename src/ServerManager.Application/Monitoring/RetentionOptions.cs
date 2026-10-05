namespace ServerManager.Application.Monitoring;

/// <summary>
/// Geçmiş ve log kayıtlarının saklama süreleri (gün). 0 kaydı süresiz saklar. Denetim kayıtları (AuditLogs) zincirli
/// imza taşıdığı için hiçbir zaman silinmez ve burada yer almaz.
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Kırpılan log metninin yerine yazılan açıklama.</summary>
    public const string TrimmedLogMarker = "[Log, saklama süresi dolduğu için temizlendi.]";

    /// <summary>Süresi dolsa bile log metni korunan son deployment sayısı (proje başına).</summary>
    public const int ProtectedDeploymentLogsPerProject = 10;

    /// <summary>Süresi dolsa bile log metni korunan son yedek/geri yükleme sayısı (iş başına).</summary>
    public const int ProtectedBackupRunLogsPerJob = 10;

    /// <summary>Süresi dolsa bile log metni korunan son servis işlemi sayısı (servis başına).</summary>
    public const int ProtectedServiceOperationLogsPerService = 10;

    /// <summary>Biten deployment'ların log metni; kayıt (durum, commit, süre) korunur.</summary>
    public int DeploymentLogDays { get; set; } = 90;

    /// <summary>Biten yedek ve geri yüklemelerin log metni; kayıt ve yedek dosyası bilgisi korunur.</summary>
    public int BackupRunLogDays { get; set; } = 90;

    /// <summary>Biten toplu komut çalıştırmaları ve sunucu çıktıları birlikte silinir.</summary>
    public int CommandRunDays { get; set; } = 90;

    /// <summary>Kapanmış terminal oturumları ve komut geçmişleri birlikte silinir.</summary>
    public int TerminalSessionDays { get; set; } = 90;

    /// <summary>Çözülmüş alarm kayıtları; açık alarmlar silinmez.</summary>
    public int AlertEventDays { get; set; } = 90;

    /// <summary>Biten servis işlemlerinin (Servisler: kurulum, yükseltme, kaldırma) log metni; kayıt korunur.</summary>
    public int ServiceOperationLogDays { get; set; } = 90;

    /// <summary>Gün değerinden silme sınırını hesaplar; 0 veya negatif değer süresiz saklama demektir (null).</summary>
    public static DateTime? Cutoff(int days, DateTime nowUtc) => days <= 0 ? null : nowUtc.AddDays(-days);
}
