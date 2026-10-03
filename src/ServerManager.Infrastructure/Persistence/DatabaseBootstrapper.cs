using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ServerManager.Infrastructure.Persistence;

public static partial class DatabaseBootstrapper
{
    public static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger, CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Bağlantı dizesinde veritabanı adı (Database / Initial Catalog) tanımlı olmalıdır.");

        if (!DatabaseNamePattern().IsMatch(databaseName))
            throw new InvalidOperationException("Veritabanı adı yalnızca harf, rakam, alt çizgi ve tire içerebilir.");

        builder.InitialCatalog = "master";

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(@name) IS NULL CREATE DATABASE [{databaseName}];";
        command.Parameters.AddWithValue("@name", databaseName);
        await command.ExecuteNonQueryAsync(cancellationToken);

        logger.LogInformation("Veritabanı hazır: {Database}", databaseName);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_\-]+$")]
    private static partial Regex DatabaseNamePattern();
}
