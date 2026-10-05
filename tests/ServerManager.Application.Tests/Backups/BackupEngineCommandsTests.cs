using System.Text;
using System.Text.RegularExpressions;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Backups;

namespace ServerManager.Application.Tests.Backups;

/// <summary>MongoDB, Redis ve SQL Server komutları: parola argv'ye girmez, adlar tırnaklanır ve doğrulanır.</summary>
public class BackupEngineCommandsTests
{
    private const string Password = "Sup3r'Gizli\"$(rm -rf /)";

    private static BackupSourceSpec Spec(BackupDatabaseEngine engine, string? database = "shop", string? user = "app", string? container = "app-db") => new()
    {
        Type = BackupSourceType.Database,
        Engine = engine,
        ContainerName = container,
        DatabaseName = database,
        DatabaseUser = user,
        DatabasePassword = Password
    };

    /// <summary><c>sh -c '...'</c> veya <c>docker exec -i 'x' sh -c '...'</c> komutundaki betiği kabuğun göreceği hâliyle döner.</summary>
    private static string Script(string command)
    {
        var start = command.IndexOf("sh -c ", StringComparison.Ordinal);
        Assert.True(start >= 0, command);
        var word = command[(start + "sh -c ".Length)..];

        var result = new StringBuilder();
        var i = 0;
        while (i < word.Length && word[i] != ' ')
        {
            var quote = word[i];
            Assert.True(quote is '\'' or '"', $"Beklenmeyen karakter: {quote}");
            var end = word.IndexOf(quote, i + 1);
            result.Append(word, i + 1, end - i - 1);
            i = end + 1;
        }

        Assert.Equal(word.Length, i);
        return result.ToString();
    }

    [Theory]
    [InlineData(BackupDatabaseEngine.MongoDb)]
    [InlineData(BackupDatabaseEngine.Redis)]
    [InlineData(BackupDatabaseEngine.SqlServer)]
    public void Password_is_read_from_stdin_and_never_written_into_the_command(BackupDatabaseEngine engine)
    {
        var spec = Spec(engine);

        var export = BackupCommands.Export(spec);

        Assert.DoesNotContain("Gizli", export);
        Assert.Contains("read -r SM_DB_PASSWORD", export);
        Assert.True(BackupCommands.NeedsPasswordInput(spec));
        Assert.Equal(Password + "\n", BackupCommands.PasswordInput(spec));
        Assert.StartsWith("docker exec -i 'app-db' sh -c ", export);

        var script = Script(export);
        Assert.DoesNotContain("--password", script);
        Assert.DoesNotContain(" -a ", script);
        Assert.DoesNotContain(" -P ", script);

        if (engine != BackupDatabaseEngine.Redis)
        {
            var import = BackupCommands.Import(new BackupSourceSpec
            {
                Type = spec.Type,
                Engine = spec.Engine,
                ContainerName = spec.ContainerName,
                DatabaseName = spec.DatabaseName,
                SourceDatabaseName = spec.DatabaseName,
                DatabaseUser = spec.DatabaseUser,
                DatabasePassword = spec.DatabasePassword
            });
            Assert.DoesNotContain("Gizli", import);
            Assert.Contains("read -r SM_DB_PASSWORD", import);
        }
    }

    [Fact]
    public void Mongo_password_goes_to_a_private_temporary_config_file()
    {
        var script = Script(BackupCommands.Export(Spec(BackupDatabaseEngine.MongoDb)));

        Assert.Contains("SMT=$(mktemp -d)", script);
        Assert.Contains("trap 'rm -rf \"$SMT\"' EXIT", script);
        Assert.Contains("(umask 077 && printf \"password: '%s'\\n\" \"$SMP\" > \"$SMT/tools.yml\")", script);
        Assert.Contains("sed \"s/'/''/g\"", script);
        Assert.Contains("mongodump --archive $SMC --username='app' --authenticationDatabase='admin' --db='shop'", script);
        Assert.Contains("| gzip -c", script);
        Assert.DoesNotContain("--quiet", script);
        Assert.DoesNotContain("--uri", script);
    }

    [Fact]
    public void Mongo_without_database_dumps_everything_and_uses_custom_auth_source()
    {
        var export = BackupCommands.Export(new BackupSourceSpec
        {
            Type = BackupSourceType.Database,
            Engine = BackupDatabaseEngine.MongoDb,
            DatabaseUser = "backup",
            DatabaseAuthSource = "users",
            DatabaseHost = "127.0.0.1",
            DatabasePort = 27018
        });

        Assert.StartsWith("sh -c '", export);
        var script = Script(export);
        Assert.Contains("mongodump --archive $SMC --host='127.0.0.1' --port=27018 --username='backup' --authenticationDatabase='users'", script);
        Assert.DoesNotContain("--db=", script);
    }

    [Fact]
    public void Mongo_without_user_does_not_authenticate()
    {
        var script = Script(BackupCommands.Export(Spec(BackupDatabaseEngine.MongoDb, user: null)));

        Assert.DoesNotContain("--username", script);
        Assert.DoesNotContain("--authenticationDatabase", script);
    }

    [Fact]
    public void Mongo_restore_renames_namespaces_and_drops_only_when_asked()
    {
        var renamed = Script(BackupCommands.Import(new BackupSourceSpec
        {
            Type = BackupSourceType.Database,
            Engine = BackupDatabaseEngine.MongoDb,
            ContainerName = "app-db",
            SourceDatabaseName = "shop",
            DatabaseName = "shop_copy",
            DropExisting = true
        }));

        Assert.Contains("mongorestore --archive $SMC --drop --nsInclude='shop.*' --nsFrom='shop.*' --nsTo='shop_copy.*'", renamed);
        Assert.Contains("{ gunzip -c; echo $? > \"$ST\"; } | mongorestore", renamed);

        var same = Script(BackupCommands.Import(new BackupSourceSpec
        {
            Type = BackupSourceType.Database,
            Engine = BackupDatabaseEngine.MongoDb,
            SourceDatabaseName = "shop",
            DatabaseName = "shop"
        }));

        Assert.DoesNotContain("--drop", same);
        Assert.DoesNotContain("--nsFrom", same);
        Assert.Contains("--nsInclude='shop.*'", same);

        var all = Script(BackupCommands.Import(new BackupSourceSpec { Type = BackupSourceType.Database, Engine = BackupDatabaseEngine.MongoDb }));
        Assert.DoesNotContain("--ns", all);
    }

    [Fact]
    public void Mongo_full_backup_cannot_be_restored_under_another_name()
    {
        Assert.Throws<ArgumentException>(() => BackupCommands.Import(new BackupSourceSpec
        {
            Type = BackupSourceType.Database,
            Engine = BackupDatabaseEngine.MongoDb,
            DatabaseName = "other"
        }));
    }

    [Theory]
    [InlineData("shop.items")]
    [InlineData("shop$x")]
    [InlineData("a b")]
    [InlineData("-x")]
    public void Mongo_rejects_unsafe_database_names(string name)
    {
        Assert.Throws<ArgumentException>(() => BackupCommands.Export(Spec(BackupDatabaseEngine.MongoDb, database: name)));
    }

    [Fact]
    public void Redis_uses_rediscli_auth_bgsave_and_streams_the_rdb_file()
    {
        var script = Script(BackupCommands.Export(Spec(BackupDatabaseEngine.Redis, database: null, user: null)));

        Assert.Contains("if [ -n \"$SM_DB_PASSWORD\" ]; then export REDISCLI_AUTH=\"$SM_DB_PASSWORD\"; fi", script);
        Assert.Contains("smr() { redis-cli \"$@\"; }", script);
        Assert.Contains("R=$(smr BGSAVE 2>&1)", script);
        Assert.Contains("L=$(smr LASTSAVE)", script);
        Assert.Contains("[ \"$N\" != \"$L\" ]", script);
        Assert.Contains("smr CONFIG GET dir", script);
        Assert.Contains("smr CONFIG GET dbfilename", script);
        Assert.Contains("rdb_last_bgsave_status", script);
        Assert.Contains("gzip -c < \"$SMP\"", script);
        Assert.DoesNotContain("--user", script);
    }

    [Fact]
    public void Redis_acl_user_host_and_port_are_quoted()
    {
        var script = Script(BackupCommands.Export(new BackupSourceSpec
        {
            Type = BackupSourceType.Database,
            Engine = BackupDatabaseEngine.Redis,
            DatabaseUser = "backup",
            DatabaseHost = "127.0.0.1",
            DatabasePort = 6380
        }));

        Assert.Contains("smr() { redis-cli -h '127.0.0.1' -p 6380 --user 'backup' \"$@\"; }", script);
    }

    [Fact]
    public void Redis_restore_is_not_supported()
    {
        var error = Assert.Throws<NotSupportedException>(() => BackupCommands.Import(Spec(BackupDatabaseEngine.Redis)));
        Assert.Contains("elle", error.Message);
    }

    [Fact]
    public void SqlServer_backup_is_copy_only_into_a_temporary_bak_that_is_removed()
    {
        var script = Script(BackupCommands.ExportSqlServer(Spec(BackupDatabaseEngine.SqlServer, user: "sa"), "abc123"));

        Assert.Contains("export SQLCMDPASSWORD=\"$SM_DB_PASSWORD\"", script);
        Assert.Contains("SMQ='/opt/mssql-tools18/bin/sqlcmd -C'", script);
        Assert.Contains("SMQ=/opt/mssql-tools/bin/sqlcmd", script);
        Assert.Contains("SMF='/var/opt/mssql/backup/sm-backup-abc123.bak'", script);
        Assert.Contains("trap 'rm -f \"$SMF\"' EXIT", script);
        Assert.Contains("$SMQ -b -S 'localhost' -U 'sa' -Q 'SET NOCOUNT ON; BACKUP DATABASE [shop] TO DISK = N'\"'\"'/var/opt/mssql/backup/sm-backup-abc123.bak'\"'\"' WITH COPY_ONLY, INIT;' >&2 || exit $?", script);
        Assert.Contains("gzip -c < \"$SMF\"", script);
        Assert.DoesNotContain("COMPRESSION", script);
    }

    [Fact]
    public void SqlServer_temporary_file_name_is_unique_per_command()
    {
        var pattern = new Regex("sm-backup-([0-9a-f]{32})\\.bak");

        var first = pattern.Match(BackupCommands.Export(Spec(BackupDatabaseEngine.SqlServer)));
        var second = pattern.Match(BackupCommands.Export(Spec(BackupDatabaseEngine.SqlServer)));

        Assert.True(first.Success);
        Assert.NotEqual(first.Groups[1].Value, second.Groups[1].Value);
        Assert.Throws<ArgumentException>(() => BackupCommands.ExportSqlServer(Spec(BackupDatabaseEngine.SqlServer), "../x"));
    }

    [Fact]
    public void SqlServer_restore_replaces_and_always_returns_to_multi_user()
    {
        var script = Script(BackupCommands.ImportSqlServer(Spec(BackupDatabaseEngine.SqlServer), "abc123"));

        Assert.Contains("(umask 077 && gunzip -c > \"$SMF\") || exit $?", script);
        Assert.Contains("chown mssql \"$SMF\"", script);
        Assert.Contains("SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [shop] FROM DISK", script);
        Assert.Contains("WITH REPLACE, RECOVERY;", script);
        Assert.Contains("SET MULTI_USER;' >&2 || true", script);
        Assert.Contains("exit \"$RC\"", script);
        Assert.DoesNotContain("MOVE", script);
    }

    [Fact]
    public void SqlServer_restore_under_a_new_name_moves_the_data_files()
    {
        var script = Script(BackupCommands.ImportSqlServer(new BackupSourceSpec
        {
            Type = BackupSourceType.Database,
            Engine = BackupDatabaseEngine.SqlServer,
            SourceDatabaseName = "shop",
            DatabaseName = "shop_copy",
            DatabaseUser = "sa"
        }, "abc123"));

        Assert.Contains("RESTORE FILELISTONLY", script);
        Assert.Contains("InstanceDefaultDataPath", script);
        Assert.Contains("MOVE", script);
        Assert.Contains("RESTORE DATABASE [shop_copy]", script);
        Assert.Contains("EXEC (@s);", script);
    }

    [Theory]
    [InlineData("shop]; DROP DATABASE master; --")]
    [InlineData("shop'x")]
    [InlineData("shop.dbo")]
    [InlineData("1shop")]
    public void SqlServer_rejects_unsafe_database_names(string name)
    {
        Assert.Throws<ArgumentException>(() => BackupCommands.Export(Spec(BackupDatabaseEngine.SqlServer, database: name)));
        Assert.Throws<ArgumentException>(() => BackupCommands.Import(Spec(BackupDatabaseEngine.SqlServer, database: name)));
    }

    [Fact]
    public void SqlServer_identifier_and_string_escaping()
    {
        Assert.Equal("[a]]b]", BackupCommands.SqlIdentifier("a]b"));
        Assert.Equal("N'a''b'", BackupCommands.SqlString("a'b"));
    }

    [Fact]
    public void SqlServer_uses_sa_when_user_is_empty_and_port_when_given()
    {
        var script = Script(BackupCommands.Export(new BackupSourceSpec
        {
            Type = BackupSourceType.Database,
            Engine = BackupDatabaseEngine.SqlServer,
            DatabaseName = "shop",
            DatabasePort = 14330
        }));

        Assert.Contains("-S 'localhost,14330' -U 'sa'", script);
    }
}
