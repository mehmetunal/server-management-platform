using Microsoft.Data.SqlClient;

namespace ServerManager.E2E.Tests.Infrastructure;

/// <summary>
/// Uçtan uca testler gerçek bir SSH sunucusu (docker compose --profile e2e: sm-e2e-ubuntu) ve SQL Server ister.
/// Gerekli ortam değişkenleri tanımlı değilse testler atlanır:
/// <list type="bullet">
/// <item><c>SM_TEST_SQL</c> — veritabanı adı içermeyen SQL Server bağlantı dizesi (her koşu geçici veritabanı açar).</item>
/// <item><c>SM_E2E_SSH_HOST</c>, <c>SM_E2E_SSH_PORT</c>, <c>SM_E2E_SSH_USER</c>, <c>SM_E2E_SSH_PASSWORD</c> — parolalı sudo kullanıcısı.</item>
/// <item><c>SM_E2E_SSH_NOPASSWD_USER</c> (isteğe bağlı) — aynı parolalı, NOPASSWD sudo kullanıcısı.</item>
/// </list>
/// </summary>
public static class E2EEnvironment
{
    public const string SkipReason =
        "Uçtan uca testler atlandı: SM_TEST_SQL ve SM_E2E_SSH_HOST/PORT/USER/PASSWORD tanımlı değil " +
        "(docker compose -p sm-e2e --profile e2e up -d --build sm-e2e-ubuntu; README 'Uçtan uca testler').";

    public static string? SqlServer => Get("SM_TEST_SQL");

    public static string? Host => Get("SM_E2E_SSH_HOST");

    public static int Port => int.TryParse(Get("SM_E2E_SSH_PORT"), out var port) ? port : 2224;

    public static string? User => Get("SM_E2E_SSH_USER");

    public static string? Password => Get("SM_E2E_SSH_PASSWORD");

    public static string? NoPasswordSudoUser => Get("SM_E2E_SSH_NOPASSWD_USER");

    public static bool IsConfigured =>
        SqlServer is not null && Host is not null && Get("SM_E2E_SSH_PORT") is not null && User is not null && Password is not null;

    public static string ConnectionStringFor(string databaseName) =>
        new SqlConnectionStringBuilder(SqlServer) { InitialCatalog = databaseName }.ConnectionString;

    public static async Task DropDatabaseAsync(string databaseName)
    {
        if (SqlServer is null)
            return;

        var builder = new SqlConnectionStringBuilder(SqlServer) { InitialCatalog = "master", Pooling = false };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
        command.Parameters.AddWithValue("@name", databaseName);
        await command.ExecuteNonQueryAsync();
    }

    private static string? Get(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}
