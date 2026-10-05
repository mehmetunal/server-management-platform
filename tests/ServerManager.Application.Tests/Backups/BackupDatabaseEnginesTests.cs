using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Backups;

public class BackupDatabaseEnginesTests
{
    private static readonly Guid JobId = Guid.Parse("0f8e5b7a-1c2d-4e3f-9a8b-7c6d5e4f3a2b");
    private static readonly Guid RunId = Guid.Parse("a1b2c3d4-0000-0000-0000-000000000000");
    private static readonly DateTime StartedAt = new(2026, 3, 10, 3, 0, 5, DateTimeKind.Utc);

    [Theory]
    [InlineData(BackupDatabaseEngine.PostgreSql, "PostgreSQL", 5432, "postgres")]
    [InlineData(BackupDatabaseEngine.MySql, "MySQL / MariaDB", 3306, "root")]
    [InlineData(BackupDatabaseEngine.MongoDb, "MongoDB", 27017, "root")]
    [InlineData(BackupDatabaseEngine.Redis, "Redis", 6379, null)]
    [InlineData(BackupDatabaseEngine.SqlServer, "SQL Server", 1433, "sa")]
    public void Engine_names_and_defaults(BackupDatabaseEngine engine, string name, int port, string? user)
    {
        Assert.Equal(name, BackupSourceDescriber.EngineName(engine));
        Assert.Equal(port, BackupDatabaseEngines.DefaultPort(engine));
        Assert.Equal(user, BackupDatabaseEngines.DefaultUser(engine));
    }

    [Fact]
    public void Only_redis_restore_is_manual()
    {
        Assert.False(BackupDatabaseEngines.SupportsRestore(BackupDatabaseEngine.Redis));
        Assert.False(BackupDatabaseEngines.SupportsRestore(null));
        Assert.True(BackupDatabaseEngines.SupportsRestore(BackupDatabaseEngine.MongoDb));
        Assert.True(BackupDatabaseEngines.SupportsRestore(BackupDatabaseEngine.SqlServer));
        Assert.Contains("AOF", BackupDatabaseEngines.RedisManualRestoreGuidance);
    }

    [Theory]
    [InlineData(BackupDatabaseEngine.MongoDb, "shop", "MongoDB: shop (container: mongo)")]
    [InlineData(BackupDatabaseEngine.MongoDb, null, "MongoDB: tüm veritabanları (container: mongo)")]
    [InlineData(BackupDatabaseEngine.Redis, null, "Redis: tüm veritabanları (RDB) (container: mongo)")]
    [InlineData(BackupDatabaseEngine.SqlServer, "Shop", "SQL Server: Shop (container: mongo)")]
    public void Describer_summarizes_new_engines(BackupDatabaseEngine engine, string? database, string expected)
    {
        var job = new BackupJob
        {
            SourceType = BackupSourceType.Database,
            DatabaseEngine = engine,
            DatabaseName = database,
            ContainerName = "mongo"
        };

        Assert.Equal(expected, BackupSourceDescriber.Describe(job));
    }

    [Theory]
    [InlineData(BackupDatabaseEngine.PostgreSql, ".sql.gz")]
    [InlineData(BackupDatabaseEngine.MongoDb, ".archive.gz")]
    [InlineData(BackupDatabaseEngine.Redis, ".rdb.gz")]
    [InlineData(BackupDatabaseEngine.SqlServer, ".bak.gz.smbk")]
    public void Object_keys_use_engine_specific_extensions_and_stay_deletable(BackupDatabaseEngine engine, string extension)
    {
        var encrypted = extension.EndsWith(".smbk", StringComparison.Ordinal);
        var key = BackupNames.ObjectKey(JobId, RunId, StartedAt, BackupSourceType.Database, encrypted, engine);

        Assert.EndsWith(extension, key);
        Assert.True(BackupNames.IsJobObjectKey(JobId, key));
        Assert.EndsWith(extension, BackupNames.FileName("App DB", StartedAt, BackupSourceType.Database, encrypted, engine));
    }

    [Fact]
    public void Container_request_maps_to_a_database_job_form_with_engine_defaults()
    {
        var serverId = Guid.NewGuid();
        var storageId = Guid.NewGuid();

        var form = BackupJobSpecs.ForContainerDatabase(new ContainerDatabaseBackupRequest(
            "mssql yedeği", serverId, storageId, BackupDatabaseEngine.SqlServer, "mssql-1", "Shop", null, "S3cret!")
        {
            ScheduleType = BackupScheduleType.Weekly,
            ScheduleDayOfWeek = DayOfWeek.Monday,
            EncryptionPassphrase = "uzun-bir-parola-123"
        });

        Assert.Equal(BackupSourceType.Database, form.SourceType);
        Assert.Equal(BackupDatabaseEngine.SqlServer, form.DatabaseEngine);
        Assert.Equal("mssql-1", form.ContainerName);
        Assert.Equal("Shop", form.DatabaseName);
        Assert.Equal("sa", form.DatabaseUser);
        Assert.Equal("S3cret!", form.DatabasePassword);
        Assert.Equal(serverId, form.ServerId);
        Assert.Equal(storageId, form.StorageId);
        Assert.True(form.EncryptionEnabled);
        Assert.Equal(form.Passphrase, form.PassphraseConfirm);
        Assert.Equal(BackupScheduleType.Weekly, form.ScheduleType);
        Assert.Equal(DayOfWeek.Monday, form.ScheduleDayOfWeek);
        Assert.Null(form.DatabaseAuthSource);
    }

    [Fact]
    public void Container_request_without_passphrase_disables_encryption_and_drops_unused_fields()
    {
        var redis = BackupJobSpecs.ForContainerDatabase(new ContainerDatabaseBackupRequest(
            "redis", Guid.NewGuid(), Guid.NewGuid(), BackupDatabaseEngine.Redis, "redis-1", "0", null, null) { AuthDatabase = "admin" });

        Assert.False(redis.EncryptionEnabled);
        Assert.Null(redis.Passphrase);
        Assert.Null(redis.DatabaseName);
        Assert.Null(redis.DatabaseUser);
        Assert.Null(redis.DatabasePassword);
        Assert.Null(redis.DatabaseAuthSource);

        var mongo = BackupJobSpecs.ForContainerDatabase(new ContainerDatabaseBackupRequest(
            "mongo", Guid.NewGuid(), Guid.NewGuid(), BackupDatabaseEngine.MongoDb, "mongo-1", null, "backup", "pw") { AuthDatabase = "admin" });

        Assert.Equal("admin", mongo.DatabaseAuthSource);
        Assert.Equal("backup", mongo.DatabaseUser);
        Assert.Null(mongo.DatabaseName);
    }
}
