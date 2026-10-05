using System.Text.RegularExpressions;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Validators.Backups;

/// <summary>Betiklere tırnaklanarak giren değerler yine de dar bir karakter kümesiyle sınırlandırılır.</summary>
public static class BackupInputPatterns
{
    public const string DockerName = "^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,254}$";
    public const string DatabaseName = "^[A-Za-z0-9_$][A-Za-z0-9_.$-]{0,127}$";
    public const string DatabaseUser = "^[A-Za-z0-9_][A-Za-z0-9_.@-]{0,127}$";
    public const string Host = "^[A-Za-z0-9_\\[][A-Za-z0-9_.:\\[\\]-]{0,254}$";

    /// <summary>MongoDB veritabanı adı: '.', '$', '/', boşluk ve tırnak içeremez; en fazla 63 karakter.</summary>
    public const string MongoDatabaseName = "^[A-Za-z0-9_][A-Za-z0-9_-]{0,62}$";

    /// <summary>
    /// SQL Server veritabanı adı: T-SQL'e <c>[ad]</c> ve <c>N'ad'</c> olarak girer; köşeli parantez, tırnak, nokta ve boşluk kabul edilmez.
    /// </summary>
    public const string SqlServerDatabaseName = "^[A-Za-z_][A-Za-z0-9_@#$-]{0,127}$";

    public const string MessageDatabaseName = "Veritabanı adı yalnızca harf, rakam, '_', '-', '.', '$' içerebilir ve '-' ile başlayamaz.";
    public const string MessageMongoDatabaseName = "MongoDB veritabanı adı yalnızca harf, rakam, '_', '-' içerebilir, '-' ile başlayamaz ve en fazla 63 karakter olabilir.";
    public const string MessageSqlServerDatabaseName = "SQL Server veritabanı adı harf veya '_' ile başlamalı; yalnızca harf, rakam, '_', '-', '@', '#', '$' içerebilir.";

    /// <summary>Motora göre veritabanı adı kalıbı.</summary>
    public static string DatabaseNamePattern(BackupDatabaseEngine? engine) => engine switch
    {
        BackupDatabaseEngine.MongoDb => MongoDatabaseName,
        BackupDatabaseEngine.SqlServer => SqlServerDatabaseName,
        _ => DatabaseName
    };

    public static string DatabaseNameMessage(BackupDatabaseEngine? engine) => engine switch
    {
        BackupDatabaseEngine.MongoDb => MessageMongoDatabaseName,
        BackupDatabaseEngine.SqlServer => MessageSqlServerDatabaseName,
        _ => MessageDatabaseName
    };

    public static bool IsValidDatabaseName(BackupDatabaseEngine? engine, string? name) =>
        name is not null && Regex.IsMatch(name, DatabaseNamePattern(engine), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>SQL Server'da veritabanı yerel olmalıdır (BACKUP/RESTORE dosyayı SQL Server'ın diskinde okur/yazar).</summary>
    public static bool IsLocalHost(string? host) =>
        string.IsNullOrWhiteSpace(host)
        || host.Trim() is "localhost" or "127.0.0.1" or "::1" or "[::1]";

    /// <summary>
    /// Parola betiğe stdin'in ilk satırı olarak verilir; satır sonu veya NUL içerirse kalanı dump/geri yükleme verisine karışır.
    /// </summary>
    public static bool IsValidDatabasePassword(string? password) =>
        password is null || password.IndexOfAny(['\r', '\n', '\0']) < 0;
}
