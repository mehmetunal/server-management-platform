namespace ServerManager.Domain.Enums;

public enum BackupDatabaseEngine
{
    PostgreSql = 1,

    /// <summary>MySQL ve MariaDB (mariadb-dump varsa o kullanılır).</summary>
    MySql = 2,

    /// <summary>mongodump arşivi (tek veritabanı veya tümü); mongorestore ile geri yüklenir.</summary>
    MongoDb = 3,

    /// <summary>BGSAVE ile alınan RDB anlık görüntüsü. Geri yükleme elle yapılır.</summary>
    Redis = 4,

    /// <summary>Microsoft SQL Server; sqlcmd ile COPY_ONLY .bak alınır ve RESTORE ... WITH REPLACE ile geri yüklenir.</summary>
    SqlServer = 5
}
