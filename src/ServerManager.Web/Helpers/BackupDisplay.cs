using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;
using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class BackupDisplay
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string StatusText(BackupRunStatus status) => status switch
    {
        BackupRunStatus.Running => "Sürüyor",
        BackupRunStatus.Succeeded => "Başarılı",
        BackupRunStatus.Failed => "Başarısız",
        BackupRunStatus.Cancelled => "İptal edildi",
        BackupRunStatus.Interrupted => "Kesildi",
        _ => status.ToString()
    };

    public static string StatusBadgeClass(BackupRunStatus? status) => status switch
    {
        BackupRunStatus.Running => "badge-info",
        BackupRunStatus.Succeeded => "badge-success",
        BackupRunStatus.Failed => "badge-danger",
        BackupRunStatus.Cancelled or BackupRunStatus.Interrupted => "badge-warning",
        _ => "badge-neutral"
    };

    public static IEnumerable<SelectListItem> StatusOptions(BackupRunStatus? selected) =>
        Enum.GetValues<BackupRunStatus>().Select(s => new SelectListItem(StatusText(s), ((int)s).ToString(), s == selected));

    public static string OperationText(BackupOperation operation) => operation switch
    {
        BackupOperation.Backup => "Yedekleme",
        BackupOperation.Restore => "Geri yükleme",
        _ => operation.ToString()
    };

    public static IEnumerable<SelectListItem> OperationOptions(BackupOperation? selected) =>
        Enum.GetValues<BackupOperation>().Select(o => new SelectListItem(OperationText(o), ((int)o).ToString(), o == selected));

    public static string TriggerText(BackupTrigger trigger) => trigger switch
    {
        BackupTrigger.Manual => "Elle",
        BackupTrigger.Scheduled => "Zamanlanmış",
        _ => trigger.ToString()
    };

    public static string SourceTypeText(BackupSourceType type) => BackupSourceDescriber.TypeName(type);

    public static string SourceTypeIcon(BackupSourceType type) => type switch
    {
        BackupSourceType.Files => "folder",
        BackupSourceType.DockerVolume => "cube",
        BackupSourceType.Database => "database",
        _ => "archive"
    };

    public static IEnumerable<SelectListItem> SourceTypeOptions(BackupSourceType selected) =>
        Enum.GetValues<BackupSourceType>().Select(t => new SelectListItem(SourceTypeText(t), ((int)t).ToString(), t == selected));

    public static string EngineText(BackupDatabaseEngine? engine) => BackupSourceDescriber.EngineName(engine);

    public static IEnumerable<SelectListItem> EngineOptions(BackupDatabaseEngine selected) =>
        Enum.GetValues<BackupDatabaseEngine>().Select(e => new SelectListItem(EngineText(e), ((int)e).ToString(), e == selected));

    /// <summary>İş formunda <c>data-engine-mode</c> değeri (boşlukla ayrılmış motor numaraları).</summary>
    public static string EngineModes(params BackupDatabaseEngine[] engines) =>
        string.Join(' ', engines.Select(e => ((int)e).ToString(CultureInfo.InvariantCulture)));

    /// <summary>Motor seçilince kullanıcı ve port alanlarına yazılan örnekler.</summary>
    public static string EngineDefaultsJson() =>
        JsonSerializer.Serialize(Enum.GetValues<BackupDatabaseEngine>().ToDictionary(
            e => ((int)e).ToString(CultureInfo.InvariantCulture),
            e => new { port = BackupDatabaseEngines.DefaultPort(e), user = BackupDatabaseEngines.DefaultUser(e) ?? string.Empty }));

    public static string EngineHint(BackupDatabaseEngine engine) => engine switch
    {
        BackupDatabaseEngine.PostgreSql => "pg_dump ile düz SQL dökümü alınır (--clean --if-exists); psql ile geri yüklenir. Varsayılan port 5432.",
        BackupDatabaseEngine.MySql => "mariadb-dump (yoksa mysqldump) ile --single-transaction dökümü alınır; mariadb / mysql ile geri yüklenir. Varsayılan port 3306.",
        BackupDatabaseEngine.MongoDb => "mongodump --archive ile alınıp gzip'lenir; mongorestore --archive ile geri yüklenir. Parola, yalnızca sahibinin okuyabildiği geçici bir config dosyasıyla verilir (MongoDB Database Tools 100.3+). Varsayılan port 27017.",
        BackupDatabaseEngine.Redis => "BGSAVE ile yeni bir RDB anlık görüntüsü alınır ve dosya gzip'lenir (AOF açık olsa da RDB tüm veriyi içerir). Redis bu sunucuda çalışmalı, CONFIG komutu açık olmalıdır. Geri yükleme panelden yapılmaz, elle yapılır. Varsayılan port 6379.",
        BackupDatabaseEngine.SqlServer => $"sqlcmd ile BACKUP DATABASE … WITH COPY_ONLY alınır; geçici .bak {BackupDatabaseEngines.SqlServerBackupDirectory} altına yazılır, aktarılır ve silinir (veritabanı kadar boş alan gerekir). SQL Server bu sunucuda çalışmalıdır. Geri yükleme RESTORE … WITH REPLACE ile yapılır. Varsayılan port 1433, kullanıcı sa.",
        _ => string.Empty
    };

    public static string RestoreDatabaseHint(BackupDatabaseEngine? engine, bool allDatabases) => engine switch
    {
        BackupDatabaseEngine.MongoDb when allDatabases => "Yedek tüm veritabanlarını içerir; özgün adlarıyla yüklenir.",
        BackupDatabaseEngine.MongoDb => "Yoksa oluşturulur. Farklı bir ad yazarsanız koleksiyonlar o veritabanına yüklenir (--nsFrom/--nsTo).",
        BackupDatabaseEngine.SqlServer => "Yoksa oluşturulur, varsa tamamen değiştirilir. Farklı bir ad yazarsanız özgün veritabanına dokunulmaz (dosyalar yeni adla taşınır).",
        _ => $"Veritabanı önceden var olmalıdır. {EngineText(engine)}"
    };

    public static string ScheduleTypeText(BackupScheduleType type) => type switch
    {
        BackupScheduleType.Manual => "Yalnızca elle",
        BackupScheduleType.Hourly => "Saatlik",
        BackupScheduleType.Daily => "Günlük",
        BackupScheduleType.Weekly => "Haftalık",
        _ => type.ToString()
    };

    public static IEnumerable<SelectListItem> ScheduleTypeOptions(BackupScheduleType selected) =>
        Enum.GetValues<BackupScheduleType>().Select(t => new SelectListItem(ScheduleTypeText(t), ((int)t).ToString(), t == selected));

    public static string DayText(DayOfWeek day) => Turkish.DateTimeFormat.GetDayName(day);

    public static IEnumerable<SelectListItem> DayOptions(DayOfWeek selected) =>
        new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Select(d => new SelectListItem(DayText(d), ((int)d).ToString(), d == selected));

    public static string Schedule(BackupJobListItemDto job)
    {
        var time = $"{job.ScheduleMinuteOfDay / 60:00}:{job.ScheduleMinuteOfDay % 60:00}";
        return job.ScheduleType switch
        {
            BackupScheduleType.Manual => "Yalnızca elle",
            BackupScheduleType.Hourly => job.ScheduleIntervalHours == 1 ? "Her saat" : $"{job.ScheduleIntervalHours} saatte bir",
            BackupScheduleType.Daily => $"Her gün {time}",
            BackupScheduleType.Weekly => $"Her {DayText(job.ScheduleDayOfWeek ?? DayOfWeek.Sunday)} {time}",
            _ => "—"
        };
    }

    public static string Retention(BackupJobListItemDto job) =>
        job.KeepDays > 0 ? $"Son {job.KeepLast} yedek + {job.KeepDays} gün" : $"Son {job.KeepLast} yedek";

    public static string Size(long? bytes) => bytes is { } value ? BackupRunLog.FormatSize(value) : "—";
}
