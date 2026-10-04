using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Backups;

namespace ServerManager.Application.Tests.Backups;

public class BackupCommandsTests
{
    private const string Password = "Sup3r'Gizli\"$(rm -rf /)";

    private static BackupSourceSpec Database(BackupDatabaseEngine engine, string? container = "app-db") => new()
    {
        Type = BackupSourceType.Database,
        Engine = engine,
        ContainerName = container,
        DatabaseName = "shop",
        DatabaseUser = "shop_user",
        DatabasePassword = Password
    };

    [Theory]
    [InlineData(BackupDatabaseEngine.PostgreSql)]
    [InlineData(BackupDatabaseEngine.MySql)]
    public void Database_password_is_sent_on_stdin_and_never_in_the_command(BackupDatabaseEngine engine)
    {
        var spec = Database(engine);

        var export = BackupCommands.Export(spec);
        var import = BackupCommands.Import(spec);

        Assert.DoesNotContain("Gizli", export);
        Assert.DoesNotContain("Gizli", import);
        Assert.True(BackupCommands.NeedsPasswordInput(spec));
        Assert.Equal(Password + "\n", BackupCommands.PasswordInput(spec));
        Assert.Contains("read -r SM_DB_PASSWORD", export);
        Assert.Contains("read -r SM_DB_PASSWORD", import);
    }

    [Theory]
    [InlineData(BackupDatabaseEngine.PostgreSql, "PGPASSWORD")]
    [InlineData(BackupDatabaseEngine.MySql, "MYSQL_PWD")]
    public void Container_database_commands_are_a_single_docker_exec(BackupDatabaseEngine engine, string variable)
    {
        var export = BackupCommands.Export(Database(engine));
        var import = BackupCommands.Import(Database(engine));

        Assert.StartsWith("docker exec -i 'app-db' sh -c ", export);
        Assert.StartsWith("docker exec -i 'app-db' sh -c ", import);
        Assert.Contains($"export {variable}=", export);
        Assert.DoesNotContain("-e " + variable, export);
        Assert.Null(BackupCommands.Precheck(Database(engine)));
    }

    [Fact]
    public void Postgres_dump_is_portable_and_gzipped()
    {
        var export = BackupCommands.Export(Database(BackupDatabaseEngine.PostgreSql, container: null));

        Assert.Contains("pg_dump", export);
        Assert.Contains("--clean --if-exists --no-owner --no-privileges", export);
        Assert.Contains("| gzip -c", export);
        Assert.DoesNotContain("docker exec", export);
        Assert.StartsWith("sh -c '", export);
    }

    [Fact]
    public void Mysql_dump_prefers_mariadb_dump_with_mysqldump_fallback()
    {
        var export = BackupCommands.Export(Database(BackupDatabaseEngine.MySql));

        Assert.Contains("mariadb-dump", export);
        Assert.Contains("mysqldump", export);
        Assert.Contains("--single-transaction", export);
    }

    [Fact]
    public void Files_export_checks_paths_and_quotes_excludes()
    {
        var export = BackupCommands.Export(new BackupSourceSpec
        {
            Type = BackupSourceType.Files,
            Paths = ["/etc/nginx", "/srv/my app"],
            Excludes = ["*.log"]
        });

        Assert.Contains("tar -czf - $TW --exclude=", export);
        Assert.Contains("grep -q GNU", export);
        Assert.Contains("etc/nginx", export);
        Assert.Contains("srv/my app", export);
        Assert.Contains("--exclude=", export);
        Assert.Contains("exit 3", export);
    }

    [Fact]
    public void Volume_commands_use_a_helper_container_without_logs_or_network()
    {
        var spec = new BackupSourceSpec { Type = BackupSourceType.DockerVolume, VolumeName = "app_data" };

        var export = BackupCommands.Export(spec);
        var import = BackupCommands.Import(spec);

        Assert.Equal("docker run --rm --network none --log-driver none -v 'app_data:/volume:ro' --entrypoint tar alpine:3 -czf - -C /volume .", export);
        Assert.Equal("docker run -i --rm --network none --log-driver none -v 'app_data:/volume' --entrypoint tar alpine:3 -xzpf - -C /volume", import);
        Assert.Equal("docker volume inspect --format '{{.Name}}' 'app_data'", BackupCommands.Precheck(spec));
    }

    [Fact]
    public void Files_restore_extracts_into_target_directory()
    {
        var import = BackupCommands.Import(new BackupSourceSpec { Type = BackupSourceType.Files, TargetDirectory = "/restore/test" });

        Assert.Contains("/restore/test", import);
        Assert.Contains("tar -xzpf -", import);
        Assert.False(BackupCommands.NeedsPasswordInput(new BackupSourceSpec { Type = BackupSourceType.Files }));
    }

    [Fact]
    public void Database_name_with_shell_characters_is_quoted()
    {
        var spec = new BackupSourceSpec
        {
            Type = BackupSourceType.Database,
            Engine = BackupDatabaseEngine.PostgreSql,
            DatabaseName = "shop$test",
            DatabaseUser = "u",
            DatabaseHost = "db.local",
            DatabasePort = 5433
        };

        var export = BackupCommands.Export(spec);

        Assert.Contains("shop$test", export);
        Assert.Contains("-p 5433", export);
        Assert.StartsWith("sh -c '", export);
    }
}
