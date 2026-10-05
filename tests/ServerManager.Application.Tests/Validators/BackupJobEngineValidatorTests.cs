using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Validators.Backups;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Validators;

/// <summary>Motora göre değişen zorunlu alanlar ve ad kuralları.</summary>
public class BackupJobEngineValidatorTests
{
    private readonly BackupJobFormDtoValidator _validator = new();

    private static BackupJobFormDto Database(BackupDatabaseEngine engine, string? name = "shop", string? user = "app") => new()
    {
        Name = "Veritabanı",
        ServerId = Guid.NewGuid(),
        StorageId = Guid.NewGuid(),
        SourceType = BackupSourceType.Database,
        DatabaseEngine = engine,
        ContainerName = "app-db",
        DatabaseName = name,
        DatabaseUser = user,
        EncryptionEnabled = false,
        ScheduleType = BackupScheduleType.Daily,
        ScheduleTime = "03:00",
        KeepLast = 7
    };

    private bool HasError(BackupJobFormDto dto, string property) =>
        _validator.Validate(dto).Errors.Any(e => e.PropertyName == property);

    [Theory]
    [InlineData(BackupDatabaseEngine.PostgreSql)]
    [InlineData(BackupDatabaseEngine.MySql)]
    [InlineData(BackupDatabaseEngine.MongoDb)]
    [InlineData(BackupDatabaseEngine.Redis)]
    [InlineData(BackupDatabaseEngine.SqlServer)]
    public void Typical_container_job_is_valid(BackupDatabaseEngine engine)
    {
        Assert.True(_validator.Validate(Database(engine)).IsValid);
    }

    [Theory]
    [InlineData(BackupDatabaseEngine.PostgreSql, true)]
    [InlineData(BackupDatabaseEngine.MySql, true)]
    [InlineData(BackupDatabaseEngine.SqlServer, true)]
    [InlineData(BackupDatabaseEngine.MongoDb, false)]
    [InlineData(BackupDatabaseEngine.Redis, false)]
    public void Database_name_and_user_are_required_per_engine(BackupDatabaseEngine engine, bool required)
    {
        var dto = Database(engine, name: null, user: null);

        Assert.Equal(required, HasError(dto, nameof(BackupJobFormDto.DatabaseName)));
        Assert.Equal(required, HasError(dto, nameof(BackupJobFormDto.DatabaseUser)));
    }

    [Fact]
    public void Redis_ignores_database_name_and_accepts_password_without_user()
    {
        var dto = Database(BackupDatabaseEngine.Redis, name: "anything goes; here", user: null);
        dto.DatabasePassword = "secret";

        Assert.True(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void Mongo_password_requires_a_user()
    {
        var dto = Database(BackupDatabaseEngine.MongoDb, user: null);
        dto.DatabasePassword = "secret";
        Assert.True(HasError(dto, nameof(BackupJobFormDto.DatabaseUser)));

        dto.DatabasePassword = null;
        dto.HasStoredDatabasePassword = true;
        Assert.True(HasError(dto, nameof(BackupJobFormDto.DatabaseUser)));

        dto.ClearDatabasePassword = true;
        Assert.False(HasError(dto, nameof(BackupJobFormDto.DatabaseUser)));
    }

    [Theory]
    [InlineData("shop.items")]
    [InlineData("shop$")]
    [InlineData("a-very-long-name-that-goes-beyond-the-sixty-three-character-limit-x")]
    public void Mongo_database_name_is_strict(string name)
    {
        Assert.True(HasError(Database(BackupDatabaseEngine.MongoDb, name), nameof(BackupJobFormDto.DatabaseName)));
    }

    [Theory]
    [InlineData("admin", false)]
    [InlineData("my_users", false)]
    [InlineData("admin.x", true)]
    [InlineData("-x", true)]
    public void Mongo_auth_source_is_validated(string authSource, bool expectedError)
    {
        var dto = Database(BackupDatabaseEngine.MongoDb);
        dto.DatabaseAuthSource = authSource;

        Assert.Equal(expectedError, HasError(dto, nameof(BackupJobFormDto.DatabaseAuthSource)));
    }

    [Theory]
    [InlineData("Shop_DB", false)]
    [InlineData("_shop#1", false)]
    [InlineData("shop.dbo", true)]
    [InlineData("1shop", true)]
    [InlineData("shop]x", true)]
    [InlineData("shop'x", true)]
    [InlineData("shop x", true)]
    public void SqlServer_database_name_is_an_identifier(string name, bool expectedError)
    {
        Assert.Equal(expectedError, HasError(Database(BackupDatabaseEngine.SqlServer, name, "sa"), nameof(BackupJobFormDto.DatabaseName)));
    }

    [Theory]
    [InlineData(BackupDatabaseEngine.SqlServer, "db.example.com", true)]
    [InlineData(BackupDatabaseEngine.SqlServer, "127.0.0.1", false)]
    [InlineData(BackupDatabaseEngine.SqlServer, "localhost", false)]
    [InlineData(BackupDatabaseEngine.Redis, "10.0.0.5", true)]
    [InlineData(BackupDatabaseEngine.MongoDb, "10.0.0.5", false)]
    [InlineData(BackupDatabaseEngine.PostgreSql, "10.0.0.5", false)]
    public void File_based_engines_require_a_local_server(BackupDatabaseEngine engine, string host, bool expectedError)
    {
        var dto = Database(engine);
        dto.ContainerName = null;
        dto.DatabaseHost = host;

        Assert.Equal(expectedError, HasError(dto, nameof(BackupJobFormDto.DatabaseHost)));
    }
}
