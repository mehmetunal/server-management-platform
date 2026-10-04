namespace ServerManager.Application.Settings;

public static class PanelSettingCatalog
{
    public static readonly IReadOnlyList<PanelSettingDefinition> All =
    [
        Def("Monitoring:Enabled", "İzleme", "Otomatik toplama", PanelSettingKind.Boolean, "Kapalıyken periyodik SSH ölçümü durur.", cluster: "Toplama"),
        Def("Monitoring:IntervalSeconds", "İzleme", "Aralık (saniye)", PanelSettingKind.Integer, "En az 10, en fazla 3600.", 10, 3600, "Toplama"),
        Def("Monitoring:MaxConcurrency", "İzleme", "Aynı anda", PanelSettingKind.Integer, "Aynı turda ölçülen sunucu sayısı.", 1, 32, "Toplama"),
        Def("Monitoring:OfflineAfterFailures", "İzleme", "Çevrimdışı sayılması", PanelSettingKind.Integer, "Ardışık başarısız deneme.", 1, 20, "Toplama"),
        Def("Monitoring:CpuWarningPercent", "İzleme", "CPU uyarı (%)", PanelSettingKind.Number, "Kritik eşikten küçük olmalı.", 1, 99, "Eşikler"),
        Def("Monitoring:CpuCriticalPercent", "İzleme", "CPU kritik (%)", PanelSettingKind.Number, "Uyarı eşiğinden büyük olmalı.", 2, 100, "Eşikler"),
        Def("Monitoring:MemoryWarningPercent", "İzleme", "RAM uyarı (%)", PanelSettingKind.Number, "Kritik eşikten küçük olmalı.", 1, 99, "Eşikler"),
        Def("Monitoring:MemoryCriticalPercent", "İzleme", "RAM kritik (%)", PanelSettingKind.Number, "Uyarı eşiğinden büyük olmalı.", 2, 100, "Eşikler"),
        Def("Monitoring:DiskWarningPercent", "İzleme", "Disk uyarı (%)", PanelSettingKind.Number, "Kritik eşikten küçük olmalı.", 1, 99, "Eşikler"),
        Def("Monitoring:DiskCriticalPercent", "İzleme", "Disk kritik (%)", PanelSettingKind.Number, "Uyarı eşiğinden büyük olmalı.", 2, 100, "Eşikler"),
        Def("Monitoring:RawRetentionHours", "İzleme", "Ham metrik saklama (saat)", PanelSettingKind.Integer, "Ham ölçümlerin tutulduğu süre.", 1, 8760, "Saklama"),
        Def("Monitoring:HourlyRetentionDays", "İzleme", "Saatlik özet saklama (gün)", PanelSettingKind.Integer, "Saatlik özetlerin tutulduğu süre.", 1, 3650, "Saklama"),

        Def("Alerting:Enabled", "Alarmlar", "Değerlendirme", PanelSettingKind.Boolean, "Kapalıyken kural, uptime ve SSL taraması durur.", cluster: "Değerlendirme"),
        Def("Alerting:EvaluationIntervalSeconds", "Alarmlar", "Aralık (saniye)", PanelSettingKind.Integer, "En az 15.", 15, 3600, "Değerlendirme"),
        Def("Alerting:UptimeMinimumIntervalSeconds", "Alarmlar", "Uptime en az aralık (saniye)", PanelSettingKind.Integer, "Bir kontrolün yeniden çalışması için gereken süre.", 10, 86400, "Değerlendirme"),
        Def("Alerting:SslCheckIntervalHours", "Alarmlar", "SSL kontrol aralığı (saat)", PanelSettingKind.Integer, "Sertifikanın yeniden okunması.", 1, 168, "SSL"),
        Def("Alerting:SslExpiringDays", "Alarmlar", "SSL uyarı (gün kala)", PanelSettingKind.Integer, "Bu günden az kalınca alarm üretilir.", 1, 365, "SSL"),
        Def("Alerting:DeliveryRetentionDays", "Alarmlar", "Bildirim geçmişi (gün)", PanelSettingKind.Integer, "Teslim kayıtlarının tutulduğu süre.", 1, 3650, "Geçmiş"),

        Def("Backup:Enabled", "Yedekleme", "Zamanlayıcı", PanelSettingKind.Boolean, "Kapalıyken zamanı gelen iş başlatılmaz. Süren iş kesilmez.", cluster: "Zamanlayıcı"),
        Def("Backup:SchedulerIntervalSeconds", "Yedekleme", "Kontrol aralığı (saniye)", PanelSettingKind.Integer, "Zamanı gelen işlerin aranma sıklığı.", 10, 3600, "Zamanlayıcı"),
        Def("Backup:MaxConcurrency", "Yedekleme", "Aynı anda", PanelSettingKind.Integer, "Birlikte çalışan yedek sayısı.", 1, 8, "Zamanlayıcı"),
        Def("Backup:TimeZone", "Yedekleme", "Saat dilimi", PanelSettingKind.TimeZone, "Günlük ve haftalık saatler bu dilime göredir.", cluster: "Takvim"),
        Def("Backup:BackupTimeoutMinutes", "Yedekleme", "Yedek zaman aşımı (dakika)", PanelSettingKind.Integer, "Tek çalıştırmanın üst sınırı.", 1, 1440, "Süre sınırları"),
        Def("Backup:RestoreTimeoutMinutes", "Yedekleme", "Geri yükleme zaman aşımı (dakika)", PanelSettingKind.Integer, "Tek geri yüklemenin üst sınırı.", 1, 1440, "Süre sınırları"),

        Def("SecurityScan:ScanIntervalHours", "Güvenlik taraması", "Otomatik tarama (saat)", PanelSettingKind.Integer, "0 otomatik taramayı kapatır. Elle tarama durmaz.", 0, 168, "Tarama"),
        Def("SecurityScan:RetentionDays", "Güvenlik taraması", "Saklama (gün)", PanelSettingKind.Integer, "Süre dolunca eski taramalar silinir.", 1, 3650, "Tarama"),
        Def("SecurityScan:KeepLatestPerServer", "Güvenlik taraması", "Sunucu başına korunan", PanelSettingKind.Integer, "Süre dolsa da bırakılan son kayıt.", 1, 200, "Tarama"),

        Def("Cloud:SyncIntervalHours", "Bulut", "Otomatik eşitleme (saat)", PanelSettingKind.Integer, "0 otomatik eşitlemeyi kapatır. Elle eşitleme durmaz.", 0, 168, "Eşitleme")
    ];

    public static readonly IReadOnlyList<(string Warning, string Critical)> ThresholdPairs =
    [
        ("Monitoring:CpuWarningPercent", "Monitoring:CpuCriticalPercent"),
        ("Monitoring:MemoryWarningPercent", "Monitoring:MemoryCriticalPercent"),
        ("Monitoring:DiskWarningPercent", "Monitoring:DiskCriticalPercent")
    ];

    private static readonly Dictionary<string, PanelSettingDefinition> ByKey =
        All.ToDictionary(d => d.Key, StringComparer.Ordinal);

    public static bool TryGet(string key, out PanelSettingDefinition definition) => ByKey.TryGetValue(key, out definition!);

    private static PanelSettingDefinition Def(
        string key, string section, string label, PanelSettingKind kind, string hint, double minimum = 0, double maximum = 0, string? cluster = null) =>
        new(key, section, label, kind, minimum, maximum, hint, cluster);
}
