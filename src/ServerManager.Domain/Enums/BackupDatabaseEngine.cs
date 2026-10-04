namespace ServerManager.Domain.Enums;

public enum BackupDatabaseEngine
{
    PostgreSql = 1,

    /// <summary>MySQL ve MariaDB (mariadb-dump varsa o kullanılır).</summary>
    MySql = 2
}
