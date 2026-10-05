using Microsoft.Data.SqlClient;

namespace ServerManager.Web.Tests.Infrastructure;

/// <summary>
/// Entegrasyon testleri gerçek bir SQL Server ister (uygulama açılışta FluentMigrator + kimlik seed'i çalıştırır).
/// <c>SM_TEST_SQL</c> ortam değişkeni veritabanı adı içermeyen bir sunucu bağlantı dizesi olmalıdır; örn.
/// <c>Server=127.0.0.1,14340;User Id=sa;Password=...;TrustServerCertificate=True</c>.
/// Tanımlı değilse testler atlanır. Her test sunucusu kendine ait geçici bir veritabanı açar ve kapanışta siler.
/// </summary>
public static class TestDatabase
{
    public const string EnvironmentVariable = "SM_TEST_SQL";

    public const string SkipReason =
        "SM_TEST_SQL tanımlı değil; web entegrasyon testleri atlandı (docker compose up -d db ve bağlantı dizesi gerekir).";

    public static string? ServerConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } value ? value : null;

    public static bool IsConfigured => ServerConnectionString is not null;

    public static string ConnectionStringFor(string databaseName) =>
        new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = databaseName }.ConnectionString;

    public static async Task DropAsync(string databaseName)
    {
        if (!IsConfigured)
            return;

        var builder = new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = "master", Pooling = false };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
        command.Parameters.AddWithValue("@name", databaseName);
        await command.ExecuteNonQueryAsync();
    }
}
