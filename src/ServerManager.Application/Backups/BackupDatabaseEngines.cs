using ServerManager.Domain.Enums;

namespace ServerManager.Application.Backups;

/// <summary>Veritabanı motorlarına göre değişen kurallar: zorunlu alanlar, varsayılanlar, dosya uzantısı ve geri yükleme desteği.</summary>
public static class BackupDatabaseEngines
{
    public const string DefaultMongoAuthSource = "admin";

    /// <summary>SQL Server'ın geçici .bak dosyasını yazıp okuduğu klasör (resmî imajdaki varsayılan yedek klasörü).</summary>
    public const string SqlServerBackupDirectory = "/var/opt/mssql/backup";

    /// <summary>Redis yedeği elle geri yüklenir; geri yükleme sayfasında ve dokümanda bu metin gösterilir.</summary>
    public const string RedisManualRestoreGuidance =
        "Redis yedeği panelden geri yüklenmez; geri yükleme elle yapılır: " +
        "1) Dosyayı indirip açın (gunzip), 2) Redis'i durdurun, 3) RDB dosyasını veri klasörüne (CONFIG GET dir, ör. /data) dbfilename adıyla (ör. dump.rdb) kopyalayın, " +
        "4) AOF açıksa (appendonly yes) Redis RDB'yi değil AOF'u yükler: önce appendonly no ile başlatıp verinin geldiğini kontrol edin, sonra CONFIG SET appendonly yes ile AOF'u yeniden oluşturun, " +
        "5) Redis'i başlatın.";

    public static bool RequiresUser(BackupDatabaseEngine engine) =>
        engine is BackupDatabaseEngine.PostgreSql or BackupDatabaseEngine.MySql or BackupDatabaseEngine.SqlServer;

    /// <summary>MongoDB'de boş ad tüm veritabanları demektir; Redis'te ad kullanılmaz (RDB tüm veritabanlarını içerir).</summary>
    public static bool RequiresDatabaseName(BackupDatabaseEngine engine) =>
        engine is BackupDatabaseEngine.PostgreSql or BackupDatabaseEngine.MySql or BackupDatabaseEngine.SqlServer;

    public static bool UsesDatabaseName(BackupDatabaseEngine engine) => engine != BackupDatabaseEngine.Redis;

    public static bool UsesAuthSource(BackupDatabaseEngine engine) => engine == BackupDatabaseEngine.MongoDb;

    /// <summary>Redis RDB'si çalışan sunucuya güvenli biçimde yüklenemez (durdurma + dosya değiştirme + AOF); elle yapılır.</summary>
    public static bool SupportsRestore(BackupDatabaseEngine? engine) => engine is not null and not BackupDatabaseEngine.Redis;

    /// <summary>
    /// Yedek dosyası veritabanı sunucusunun kendi diskinde oluşur ve oradan okunur (SQL Server .bak, Redis RDB);
    /// uzak sunucudan akış alınamaz.
    /// </summary>
    public static bool RequiresLocalServer(BackupDatabaseEngine engine) => engine is BackupDatabaseEngine.SqlServer or BackupDatabaseEngine.Redis;

    public static int DefaultPort(BackupDatabaseEngine engine) => engine switch
    {
        BackupDatabaseEngine.PostgreSql => 5432,
        BackupDatabaseEngine.MySql => 3306,
        BackupDatabaseEngine.MongoDb => 27017,
        BackupDatabaseEngine.Redis => 6379,
        BackupDatabaseEngine.SqlServer => 1433,
        _ => 0
    };

    /// <summary>Resmî Docker imajlarındaki yönetici kullanıcı; Redis'te ACL kullanıcısı yoksa boş kalır.</summary>
    public static string? DefaultUser(BackupDatabaseEngine engine) => engine switch
    {
        BackupDatabaseEngine.PostgreSql => "postgres",
        BackupDatabaseEngine.MySql => "root",
        BackupDatabaseEngine.MongoDb => "root",
        BackupDatabaseEngine.SqlServer => "sa",
        _ => null
    };

    /// <summary>Sıkıştırılmış dökümün uzantısı (gzip sunucuda uygulanır).</summary>
    public static string FileExtension(BackupDatabaseEngine? engine) => engine switch
    {
        BackupDatabaseEngine.MongoDb => ".archive.gz",
        BackupDatabaseEngine.Redis => ".rdb.gz",
        BackupDatabaseEngine.SqlServer => ".bak.gz",
        _ => ".sql.gz"
    };
}
