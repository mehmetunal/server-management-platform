using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using ServerManager.Application.Agent;
using ServerManager.Application.Alerting;
using ServerManager.Application.Backups;
using ServerManager.Application.Cloud;
using ServerManager.Application.DTOs.Settings;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Application.Security;

namespace ServerManager.Application.Services;

public class SystemInfoService : ISystemInfoService
{
    private readonly IDatabaseInfoReader _database;
    private readonly MonitoringOptions _monitoring;
    private readonly AlertingOptions _alerting;
    private readonly BackupOptions _backup;
    private readonly SecurityScanOptions _securityScan;
    private readonly CloudOptions _cloud;

    public SystemInfoService(
        IDatabaseInfoReader database,
        IOptions<MonitoringOptions> monitoring,
        IOptions<AlertingOptions> alerting,
        IOptions<BackupOptions> backup,
        IOptions<SecurityScanOptions> securityScan,
        IOptions<CloudOptions> cloud)
    {
        _database = database;
        _monitoring = monitoring.Value;
        _alerting = alerting.Value;
        _backup = backup.Value;
        _securityScan = securityScan.Value;
        _cloud = cloud.Value;
    }

    public async Task<SystemInfoDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var version = typeof(SystemInfoService).Assembly.GetName().Version;
        return new SystemInfoDto(
            version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}",
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Environment.Version.ToString(),
            RuntimeInformation.OSDescription,
            System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            await _database.GetAsync(cancellationToken),
            Sections());
    }

    private IReadOnlyList<SettingSectionDto> Sections() =>
    [
        new("İzleme",
        [
            Item("Otomatik toplama", OnOff(_monitoring.Enabled)),
            Item("Aralık", EverySeconds(_monitoring.IntervalSeconds)),
            Item("Aynı anda", _monitoring.MaxConcurrency.ToString()),
            Item("Çevrimdışı sayılması", $"{Math.Max(1, _monitoring.OfflineAfterFailures)} başarısız deneme"),
            Item("CPU eşiği", Threshold(_monitoring.CpuWarningPercent, _monitoring.CpuCriticalPercent)),
            Item("RAM eşiği", Threshold(_monitoring.MemoryWarningPercent, _monitoring.MemoryCriticalPercent)),
            Item("Disk eşiği", Threshold(_monitoring.DiskWarningPercent, _monitoring.DiskCriticalPercent)),
            Item("Ham metrik saklama", $"{_monitoring.RawRetentionHours} saat"),
            Item("Saatlik özet saklama", $"{_monitoring.HourlyRetentionDays} gün")
        ]),
        new("Alarmlar",
        [
            Item("Değerlendirme", OnOff(_alerting.Enabled)),
            Item("Aralık", EverySeconds(_alerting.EvaluationIntervalSeconds)),
            Item("Uptime en az aralık", EverySeconds(_alerting.UptimeMinimumIntervalSeconds)),
            Item("SSL kontrol aralığı", EveryHours(_alerting.SslCheckIntervalHours)),
            Item("SSL uyarı", $"{_alerting.SslExpiringDays} gün kala"),
            Item("Bildirim geçmişi", $"{_alerting.DeliveryRetentionDays} gün")
        ]),
        new("Yedekleme",
        [
            Item("Zamanlayıcı", OnOff(_backup.Enabled)),
            Item("Kontrol aralığı", EverySeconds(_backup.SchedulerIntervalSeconds)),
            Item("Aynı anda", _backup.MaxConcurrency.ToString()),
            Item("Saat dilimi", _backup.TimeZone),
            Item("Yedek zaman aşımı", $"{_backup.BackupTimeoutMinutes} dakika"),
            Item("Geri yükleme zaman aşımı", $"{_backup.RestoreTimeoutMinutes} dakika")
        ]),
        new("Güvenlik taraması",
        [
            Item("Otomatik tarama", EveryHours(_securityScan.ScanIntervalHours)),
            Item("Saklama", $"{_securityScan.RetentionDays} gün"),
            Item("Sunucu başına korunan", $"{_securityScan.KeepLatestPerServer} kayıt")
        ]),
        new("Bulut",
        [
            Item("Otomatik eşitleme", EveryHours(_cloud.SyncIntervalHours))
        ]),
        new("Agent",
        [
            Item("Rapor aralığı", EverySeconds(AgentRules.ReportIntervalSeconds)),
            Item("En sık rapor", $"{AgentRules.MinReportIntervalSeconds} saniye"),
            Item("SSH toplamayı durdurma", $"Son rapor {AgentRules.ActiveWindow.TotalMinutes:0} dakika içindeyse"),
            Item("Sessizlik uyarısı", $"{AgentRules.SilentAfter.TotalMinutes:0} dakika")
        ])
    ];

    private static SettingItemDto Item(string label, string value) => new(label, value);

    private static string OnOff(bool enabled) => enabled ? "Açık" : "Kapalı";

    private static string Threshold(double warning, double critical) => $"Uyarı %{warning:0}, kritik %{critical:0}";

    private static string EverySeconds(int seconds) =>
        seconds <= 0 ? "Kapalı" : seconds % 3600 == 0 ? $"Her {seconds / 3600} saatte" : seconds % 60 == 0 ? $"Her {seconds / 60} dakikada" : $"Her {seconds} saniyede";

    private static string EveryHours(int hours) => hours <= 0 ? "Kapalı" : $"Her {hours} saatte";
}
