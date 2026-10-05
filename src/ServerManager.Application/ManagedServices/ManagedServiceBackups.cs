using ServerManager.Domain.Enums;

namespace ServerManager.Application.ManagedServices;

/// <summary>Veritabanı şablonlarının yedekleme motoru ve yedek kullanıcısı eşlemesi.</summary>
public static class ManagedServiceBackups
{
    /// <summary>Şablonun yedekleme motoru; uygulama şablonlarında (MinIO, n8n …) null.</summary>
    public static BackupDatabaseEngine? EngineFor(string? templateKey) => templateKey switch
    {
        ServiceTemplates.Postgres => BackupDatabaseEngine.PostgreSql,
        ServiceTemplates.MySql or ServiceTemplates.MariaDb => BackupDatabaseEngine.MySql,
        ServiceTemplates.MongoDb => BackupDatabaseEngine.MongoDb,
        ServiceTemplates.Redis => BackupDatabaseEngine.Redis,
        ServiceTemplates.SqlServer => BackupDatabaseEngine.SqlServer,
        _ => null
    };

    public static bool Supports(string? templateKey) => EngineFor(templateKey) is not null;

    /// <summary>
    /// Yedeği alan kullanıcı: MySQL/MariaDB'de root (parolası uygulama kullanıcısınınkiyle aynıdır, tüm yetkiler gerekir),
    /// PostgreSQL ve MongoDB'de servisin yönetici kullanıcısı, SQL Server'da sa, Redis'te yok.
    /// </summary>
    public static string? BackupUser(string templateKey, string? serviceUsername) => templateKey switch
    {
        ServiceTemplates.MySql or ServiceTemplates.MariaDb => "root",
        ServiceTemplates.Redis => null,
        ServiceTemplates.SqlServer => "sa",
        _ => serviceUsername
    };

    /// <summary>
    /// Yedeklenecek veritabanı: MongoDB'de tüm veritabanları (null), Redis'te yok; diğerlerinde servisin veritabanı,
    /// yoksa (SQL Server) formda girilen ad.
    /// </summary>
    public static string? BackupDatabase(string templateKey, string? serviceDatabase, string? requested) => templateKey switch
    {
        ServiceTemplates.MongoDb or ServiceTemplates.Redis => null,
        _ => string.IsNullOrWhiteSpace(requested) ? serviceDatabase : requested.Trim()
    };
}
