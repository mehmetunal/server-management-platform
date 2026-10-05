using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Backups;

public static class BackupSourceDescriber
{
    public static string TypeName(BackupSourceType type) => type switch
    {
        BackupSourceType.Files => "Dosya / klasör",
        BackupSourceType.DockerVolume => "Docker volume",
        BackupSourceType.Database => "Veritabanı",
        _ => type.ToString()
    };

    public static string EngineName(BackupDatabaseEngine? engine) => engine switch
    {
        BackupDatabaseEngine.PostgreSql => "PostgreSQL",
        BackupDatabaseEngine.MySql => "MySQL / MariaDB",
        BackupDatabaseEngine.MongoDb => "MongoDB",
        BackupDatabaseEngine.Redis => "Redis",
        BackupDatabaseEngine.SqlServer => "SQL Server",
        _ => "-"
    };

    /// <summary>MongoDB'de ad boşsa tüm veritabanları, Redis'te RDB tüm veritabanlarını içerir.</summary>
    public static string DatabaseLabel(BackupDatabaseEngine? engine, string? databaseName) => engine switch
    {
        BackupDatabaseEngine.Redis => "tüm veritabanları (RDB)",
        BackupDatabaseEngine.MongoDb when string.IsNullOrEmpty(databaseName) => "tüm veritabanları",
        _ => databaseName ?? "-"
    };

    public static string Describe(BackupJob job)
    {
        switch (job.SourceType)
        {
            case BackupSourceType.Files:
                var paths = BackupPaths.SplitLines(job.Paths);
                return paths.Count switch
                {
                    0 => "-",
                    1 => paths[0],
                    _ => $"{paths[0]} (+{paths.Count - 1})"
                };
            case BackupSourceType.DockerVolume:
                return $"Volume: {job.VolumeName}";
            case BackupSourceType.Database:
                var location = string.IsNullOrWhiteSpace(job.ContainerName) ? "sunucuda" : $"container: {job.ContainerName}";
                return $"{EngineName(job.DatabaseEngine)}: {DatabaseLabel(job.DatabaseEngine, job.DatabaseName)} ({location})";
            default:
                return "-";
        }
    }
}
