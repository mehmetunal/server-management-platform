using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Validators.Backups;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Backups;

/// <summary>
/// Sunucuda çalışan yedekleme/geri yükleme komutları. Çıktı gzip ile sıkıştırılır. Veritabanı parolası stdin'in ilk
/// satırından okunur, komut satırına yazılmaz. Volume ve container veritabanı komutları tek bir <c>docker</c> çağrısıdır;
/// böylece yalnızca docker grubu veya yalnızca docker için sudo yetkisi olan kullanıcılarla da çalışır.
/// Kullanıcı dosyası silinmez; geri yükleme yalnızca üzerine yazar. Silinen tek şey betiğin kendi oluşturduğu geçici dosyalardır.
/// </summary>
/// <remarks>
/// Parolanın aktarımı (hiçbiri argv'de görünmez):
/// <list type="bullet">
/// <item>PostgreSQL / MySQL: <c>PGPASSWORD</c> / <c>MYSQL_PWD</c> ortam değişkeni.</item>
/// <item>MongoDB: mongodump/mongorestore parolayı ortamdan okumaz; parola <c>mktemp -d</c> (0700) içinde 0600 izinli bir
/// YAML dosyasına (<c>password: '...'</c>) yazılır ve <c>--config</c> ile verilir; dosya çıkışta silinir.
/// MongoDB Database Tools 100.3+ gerekir.</item>
/// <item>Redis: <c>REDISCLI_AUTH</c> ortam değişkeni (<c>-a</c> kullanılmaz).</item>
/// <item>SQL Server: <c>SQLCMDPASSWORD</c> ortam değişkeni.</item>
/// </list>
/// Değişken atamaları ve <c>printf</c> kabuğun yerleşik komutlarıdır; yeni süreç başlatmaz.
/// </remarks>
public static class BackupCommands
{
    public const int SourceMissingExitCode = 3;
    public const int ToolMissingExitCode = 4;

    /// <summary>Volume içeriğini arşivleyen yardımcı container imajı (BusyBox tar içerir).</summary>
    public const string VolumeHelperImage = "alpine:3";

    public const string SqlServerBackupDirectory = BackupDatabaseEngines.SqlServerBackupDirectory;

    private const string ReadPassword = "IFS= read -r SM_DB_PASSWORD || true\n";

    /// <summary>GNU tar'a özgü seçenekler yalnızca GNU tar varsa eklenir; BusyBox (Alpine) tar da desteklenir.</summary>
    private const string DetectTar = "TW=\nif tar --version 2>/dev/null | grep -q GNU; then TW=--warning=no-file-changed; fi\n";

    /// <summary>GNU tar 1 ile çıkarsa (okunurken değişen dosyalar) yedek yine geçerlidir; BusyBox'ta 1 hatadır.</summary>
    private const string TarResult = "rc=$?\nif [ \"$rc\" -eq 1 ] && [ -n \"$TW\" ]; then exit 0; fi\nexit \"$rc\"\n";

    /// <summary>Container çıktısı Docker log dosyasına yazılmaz (büyük yedekler diski doldurmasın).</summary>
    private const string HelperRunOptions = "--rm --network none --log-driver none";

    /// <summary>Sinyalle durdurulunca da EXIT tuzağı (geçici dosya temizliği) çalışsın.</summary>
    private const string ExitOnSignal = "trap 'exit 130' HUP INT TERM\n";

    /// <summary>
    /// mssql-tools18 (sertifika doğrulaması varsayılan açık, <c>-C</c> ile yerel sertifikaya güvenilir), eski mssql-tools
    /// veya PATH'teki sqlcmd (go-sqlcmd) sırayla denenir.
    /// </summary>
    private const string DetectSqlCmd =
        "if [ -x /opt/mssql-tools18/bin/sqlcmd ]; then SMQ='/opt/mssql-tools18/bin/sqlcmd -C'\n" +
        "elif [ -x /opt/mssql-tools/bin/sqlcmd ]; then SMQ=/opt/mssql-tools/bin/sqlcmd\n" +
        "elif command -v sqlcmd >/dev/null 2>&1; then SMQ='sqlcmd -C'\n" +
        "else echo 'sqlcmd bulunamadı' >&2; exit 4; fi\n";

    public static bool NeedsPasswordInput(BackupSourceSpec spec) => spec.Type == BackupSourceType.Database;

    public static string PasswordInput(BackupSourceSpec spec) => (spec.DatabasePassword ?? string.Empty) + "\n";

    /// <summary>Dışa aktarmadan önce çalıştırılır; yoksa <c>docker run -v</c> boş bir volume oluşturup boş yedek alırdı.</summary>
    public static string? Precheck(BackupSourceSpec spec) =>
        spec.Type == BackupSourceType.DockerVolume
            ? $"docker volume inspect --format '{{{{.Name}}}}' {ShellQuote.Quote(spec.VolumeName!)}"
            : null;

    public static string Export(BackupSourceSpec spec) => spec.Type switch
    {
        BackupSourceType.Files => ExportFiles(spec),
        BackupSourceType.DockerVolume => ExportVolume(spec),
        BackupSourceType.Database => ExportDatabase(spec),
        _ => throw new ArgumentOutOfRangeException(nameof(spec))
    };

    public static string Import(BackupSourceSpec spec) => spec.Type switch
    {
        BackupSourceType.Files => ImportFiles(spec),
        BackupSourceType.DockerVolume => ImportVolume(spec),
        BackupSourceType.Database => ImportDatabase(spec),
        _ => throw new ArgumentOutOfRangeException(nameof(spec))
    };

    private static string ExportFiles(BackupSourceSpec spec)
    {
        var checks = string.Concat(spec.Paths.Select(p =>
            $"if [ ! -e {ShellQuote.Quote(p)} ]; then echo {ShellQuote.Quote("Yol bulunamadı: " + p)} >&2; exit {SourceMissingExitCode}; fi\n"));
        var excludes = string.Concat(spec.Excludes.Select(e => " --exclude=" + ShellQuote.Quote(e)));
        var names = string.Join(' ', spec.Paths.Select(p => ShellQuote.Quote(BackupPaths.ToArchiveName(p))));
        return Shell(
            checks +
            DetectTar +
            "cd / || exit 2\n" +
            $"tar -czf - $TW{excludes} -- {names}\n" +
            TarResult);
    }

    private static string ImportFiles(BackupSourceSpec spec)
    {
        var target = ShellQuote.Quote(spec.TargetDirectory ?? "/");
        return Shell(
            $"mkdir -p -- {target} || exit 2\n" +
            $"tar -xzpf - -C {target}\n");
    }

    private static string ExportVolume(BackupSourceSpec spec) =>
        $"docker run {HelperRunOptions} -v {ShellQuote.Quote(spec.VolumeName! + ":/volume:ro")} --entrypoint tar {VolumeHelperImage} -czf - -C /volume .";

    /// <summary>Volume yoksa Docker oluşturur.</summary>
    private static string ImportVolume(BackupSourceSpec spec) =>
        $"docker run -i {HelperRunOptions} -v {ShellQuote.Quote(spec.VolumeName! + ":/volume")} --entrypoint tar {VolumeHelperImage} -xzpf - -C /volume";

    private static string ExportDatabase(BackupSourceSpec spec) => spec.Engine switch
    {
        BackupDatabaseEngine.PostgreSql => Database(spec, GzipPipe(Client("pg_dump", null,
            $"--clean --if-exists --no-owner --no-privileges {PostgresConnection(spec)} -d {ShellQuote.Quote(RequiredDatabaseName(spec))}"))),
        BackupDatabaseEngine.MySql => Database(spec, GzipPipe(Client("mariadb-dump", "mysqldump",
            $"--single-transaction --routines --triggers --no-tablespaces {MySqlConnection(spec)} {ShellQuote.Quote(RequiredDatabaseName(spec))}"))),
        BackupDatabaseEngine.MongoDb => ExportMongo(spec),
        BackupDatabaseEngine.Redis => ExportRedis(spec),
        BackupDatabaseEngine.SqlServer => ExportSqlServer(spec, NewToken()),
        _ => throw new ArgumentOutOfRangeException(nameof(spec), "Bilinmeyen veritabanı motoru.")
    };

    private static string ImportDatabase(BackupSourceSpec spec) => spec.Engine switch
    {
        BackupDatabaseEngine.PostgreSql => Database(spec, RequireTool("gunzip") + "gunzip -c | " +
            Client("psql", null, $"-q -v ON_ERROR_STOP=1 {PostgresConnection(spec)} -d {ShellQuote.Quote(RequiredDatabaseName(spec))}") + "\n"),
        BackupDatabaseEngine.MySql => Database(spec, RequireTool("gunzip") + "gunzip -c | " +
            Client("mariadb", "mysql", $"{MySqlConnection(spec)} {ShellQuote.Quote(RequiredDatabaseName(spec))}") + "\n"),
        BackupDatabaseEngine.MongoDb => ImportMongo(spec),
        BackupDatabaseEngine.Redis => throw new NotSupportedException(BackupDatabaseEngines.RedisManualRestoreGuidance),
        BackupDatabaseEngine.SqlServer => ImportSqlServer(spec, NewToken()),
        _ => throw new ArgumentOutOfRangeException(nameof(spec), "Bilinmeyen veritabanı motoru.")
    };

    // ---- MongoDB ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>mongodump --archive</c> çıktısı sunucuda gzip'lenir (<c>--gzip</c> yerine): dosya düz bir .gz olur ve
    /// <c>gunzip -c dosya | mongorestore --archive</c> ile elle de açılabilir.
    /// </summary>
    private static string ExportMongo(BackupSourceSpec spec)
    {
        var database = string.IsNullOrEmpty(spec.DatabaseName) ? string.Empty : " --db=" + ShellQuote.Quote(MongoName(spec.DatabaseName));
        return Database(spec,
            RequireTool("mongodump") +
            MongoCredentials +
            // --quiet hata mesajlarını da gizler; ilerleme logları stderr'e gider, akışa karışmaz.
            GzipPipe($"mongodump --archive $SMC{MongoConnection(spec)}{database}"));
    }

    private static string ImportMongo(BackupSourceSpec spec)
    {
        var options = MongoConnection(spec);
        if (spec.DropExisting)
            options += " --drop";

        var source = string.IsNullOrEmpty(spec.SourceDatabaseName) ? null : MongoName(spec.SourceDatabaseName);
        var target = string.IsNullOrEmpty(spec.DatabaseName) ? null : MongoName(spec.DatabaseName);
        if (source is null && target is not null)
            throw new ArgumentException("Tüm veritabanlarının yedeği farklı bir ada yüklenemez.", nameof(spec));

        if (source is not null)
        {
            options += " --nsInclude=" + ShellQuote.Quote(source + ".*");
            if (target is not null && target != source)
                options += " --nsFrom=" + ShellQuote.Quote(source + ".*") + " --nsTo=" + ShellQuote.Quote(target + ".*");
        }

        return Database(spec,
            RequireTool("gunzip") +
            RequireTool("mongorestore") +
            MongoCredentials +
            ImportPipe($"mongorestore --archive $SMC{options}"));
    }

    /// <summary>
    /// Parola varsa 0700 geçici klasörde 0600 YAML dosyasına yazılır; YAML tek tırnaklı dizgede tek tırnak ikilenir.
    /// <c>printf</c> yerleşik komuttur; <c>sed</c> parolayı stdin'den okur. Dosya EXIT tuzağıyla silinir.
    /// </summary>
    private const string MongoCredentials =
        "SMT=$(mktemp -d) || exit 2\n" +
        "trap 'rm -rf \"$SMT\"' EXIT\n" +
        ExitOnSignal +
        "SMC=\n" +
        "if [ -n \"$SM_DB_PASSWORD\" ]; then\n" +
        "  SMP=$(printf '%s' \"$SM_DB_PASSWORD\" | sed \"s/'/''/g\")\n" +
        "  (umask 077 && printf \"password: '%s'\\n\" \"$SMP\" > \"$SMT/tools.yml\") || exit 2\n" +
        "  SMP=\n" +
        "  SMC=\" --config=$SMT/tools.yml\"\n" +
        "fi\n";

    private static string MongoConnection(BackupSourceSpec spec)
    {
        var parts = string.Empty;
        if (!string.IsNullOrWhiteSpace(spec.DatabaseHost))
            parts += " --host=" + ShellQuote.Quote(spec.DatabaseHost);
        if (spec.DatabasePort is { } port)
            parts += $" --port={port}";
        if (!string.IsNullOrWhiteSpace(spec.DatabaseUser))
        {
            var authSource = string.IsNullOrWhiteSpace(spec.DatabaseAuthSource) ? BackupDatabaseEngines.DefaultMongoAuthSource : spec.DatabaseAuthSource;
            parts += " --username=" + ShellQuote.Quote(spec.DatabaseUser) + " --authenticationDatabase=" + ShellQuote.Quote(MongoName(authSource));
        }

        return parts;
    }

    private static string MongoName(string name) =>
        BackupInputPatterns.IsValidDatabaseName(BackupDatabaseEngine.MongoDb, name)
            ? name
            : throw new ArgumentException(BackupInputPatterns.MessageMongoDatabaseName, nameof(name));

    // ---- Redis -----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Yeni bir RDB anlık görüntüsü alınır (BGSAVE; süren kayıt/AOF yeniden yazımı bitene kadar beklenir), LASTSAVE
    /// değişince <c>CONFIG GET dir/dbfilename</c> ile bulunan dosya gzip'lenir. AOF açık olsa da RDB tüm veriyi içerir.
    /// Dosya Redis'in diskinden okunduğu için Redis bu sunucuda (container'da veya yerel) çalışmalıdır.
    /// </summary>
    private static string ExportRedis(BackupSourceSpec spec)
    {
        var connection = string.Empty;
        if (!string.IsNullOrWhiteSpace(spec.DatabaseHost))
            connection += " -h " + ShellQuote.Quote(spec.DatabaseHost);
        if (spec.DatabasePort is { } port)
            connection += $" -p {port}";
        if (!string.IsNullOrWhiteSpace(spec.DatabaseUser))
            connection += " --user " + ShellQuote.Quote(spec.DatabaseUser);

        return Database(spec,
            RequireTool("redis-cli") +
            RequireTool("gzip") +
            $"smr() {{ redis-cli{connection} \"$@\"; }}\n" +
            "smi() { smr INFO persistence | tr -d '\\r' | sed -n \"s/^$1://p\"; }\n" +
            "smn() { case \"$1\" in ''|*[!0-9]*) echo \"Redis yanıtı: ${1:-yanıt yok}\" >&2; exit 3;; esac; }\n" +
            "SMD=$(smr CONFIG GET dir | tr -d '\\r' | sed -n 2p)\n" +
            "SMF=$(smr CONFIG GET dbfilename | tr -d '\\r' | sed -n 2p)\n" +
            "if [ -z \"$SMD\" ] || [ -z \"$SMF\" ]; then\n" +
            "  L=$(smr PING 2>&1)\n" +
            "  case \"$L\" in\n" +
            "    PONG) echo 'Redis CONFIG GET kullanılamıyor (komut kapalı, yeniden adlandırılmış veya kullanıcının yetkisi yok); RDB dosyası bulunamadı.' >&2;;\n" +
            "    *NOAUTH*|*WRONGPASS*|*invalid*password*|*invalid*username*) echo 'Redis kullanıcı adı veya parolası hatalı ya da eksik.' >&2;;\n" +
            "    *) echo \"Redis yanıtı: ${L:-yanıt yok}\" >&2;;\n" +
            "  esac\n" +
            "  exit 3\n" +
            "fi\n" +
            "case \"$SMF\" in /*) SMP=\"$SMF\";; *) SMP=\"$SMD/$SMF\";; esac\n" +
            "while [ \"$(smi rdb_bgsave_in_progress)\" = 1 ]; do sleep 1; done\n" +
            "L=$(smr LASTSAVE); smn \"$L\"\n" +
            "sleep 1\n" +
            "i=0\n" +
            "while :; do\n" +
            "  R=$(smr BGSAVE 2>&1)\n" +
            "  case \"$R\" in *started*|*scheduled*) break;; *progress*) i=$((i+1)); if [ \"$i\" -gt 3600 ]; then echo \"Redis BGSAVE başlatılamadı: $R\" >&2; exit 3; fi; sleep 1;; *) echo \"Redis BGSAVE başlatılamadı: ${R:-yanıt yok}\" >&2; exit 3;; esac\n" +
            "done\n" +
            "while :; do\n" +
            "  N=$(smr LASTSAVE); smn \"$N\"\n" +
            "  P=$(smi rdb_bgsave_in_progress)\n" +
            "  if [ \"$P\" = 0 ] && [ \"$N\" != \"$L\" ]; then break; fi\n" +
            "  if [ \"$P\" = 0 ] && [ \"$(smi rdb_last_bgsave_status)\" = err ]; then echo 'Redis BGSAVE başarısız oldu (rdb_last_bgsave_status:err); Redis loglarına bakın (disk alanı, izinler).' >&2; exit 3; fi\n" +
            "  sleep 1\n" +
            "done\n" +
            "if [ ! -r \"$SMP\" ]; then echo \"RDB dosyası okunamadı: $SMP (Redis bu sunucuda çalışmalıdır)\" >&2; exit 3; fi\n" +
            "gzip -c < \"$SMP\"\n");
    }

    // ---- SQL Server ------------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>BACKUP DATABASE ... WITH COPY_ONLY</c> (fark/log zincirini bozmaz; Express'te de çalışsın diye sıkıştırma yok)
    /// SQL Server'ın yedek klasörüne geçici .bak yazar; dosya gzip'lenip akıtılır ve silinir. sqlcmd çıktısı stderr'e gider.
    /// </summary>
    internal static string ExportSqlServer(BackupSourceSpec spec, string token)
    {
        var name = SqlServerName(RequiredDatabaseName(spec));
        var file = $"{SqlServerBackupDirectory}/sm-backup-{Token(token)}.bak";
        var sql = $"SET NOCOUNT ON; BACKUP DATABASE {SqlIdentifier(name)} TO DISK = {SqlString(file)} WITH COPY_ONLY, INIT;";

        return Database(spec,
            DetectSqlCmd +
            RequireTool("gzip") +
            SqlServerTempFile(file) +
            $"$SMQ -b {SqlServerConnection(spec)} -Q {ShellQuote.Quote(sql)} >&2 || exit $?\n" +
            "gzip -c < \"$SMF\"\n");
    }

    /// <summary>
    /// .bak geçici dosyaya açılır (sunucuda root ise mssql kullanıcısına verilir), açık bağlantılar kesilir (SINGLE_USER)
    /// ve <c>RESTORE ... WITH REPLACE</c> çalışır; ardından veritabanı her durumda MULTI_USER'a döndürülür.
    /// Aynı adla geri yüklemede veri dosyaları yedekteki yollara yazılır. Farklı adla geri yüklemede (özgün veritabanının
    /// dosyalarıyla çakışmasın diye) dosyalar <c>RESTORE FILELISTONLY</c> ile okunur ve örneğin varsayılan veri/log klasörüne
    /// <c>{ad}_{FileId}.mdf/.ndf/.ldf</c> olarak taşınır (<c>MOVE</c>).
    /// </summary>
    internal static string ImportSqlServer(BackupSourceSpec spec, string token)
    {
        var name = SqlServerName(RequiredDatabaseName(spec));
        var file = $"{SqlServerBackupDirectory}/sm-restore-{Token(token)}.bak";
        var renamed = !string.IsNullOrEmpty(spec.SourceDatabaseName) && !string.Equals(spec.SourceDatabaseName, name, StringComparison.OrdinalIgnoreCase);
        var restore =
            "SET NOCOUNT ON; " +
            (renamed ? SqlServerMoveRestore(name, file) : $"IF DB_ID({SqlString(name)}) IS NOT NULL ALTER DATABASE {SqlIdentifier(name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                                                          $"RESTORE DATABASE {SqlIdentifier(name)} FROM DISK = {SqlString(file)} WITH REPLACE, RECOVERY;");
        var multiUser = $"SET NOCOUNT ON; IF DATABASEPROPERTYEX({SqlString(name)}, 'UserAccess') = 'SINGLE_USER' ALTER DATABASE {SqlIdentifier(name)} SET MULTI_USER;";
        var connection = SqlServerConnection(spec);

        return Database(spec,
            DetectSqlCmd +
            RequireTool("gunzip") +
            SqlServerTempFile(file) +
            "(umask 077 && gunzip -c > \"$SMF\") || exit $?\n" +
            "if [ \"$(id -u)\" = 0 ] && id mssql >/dev/null 2>&1; then chown mssql \"$SMF\" || exit 2; fi\n" +
            $"$SMQ -b {connection} -Q {ShellQuote.Quote(restore)} >&2\n" +
            "RC=$?\n" +
            $"$SMQ -b {connection} -Q {ShellQuote.Quote(multiUser)} >&2 || true\n" +
            "exit \"$RC\"\n");
    }

    /// <summary>RESTORE FILELISTONLY sonuç kümesi (SQL Server 2016+; Linux sürümlerinin tümü).</summary>
    private const string FileListColumns =
        "LogicalName nvarchar(128), PhysicalName nvarchar(260), Type char(1), FileGroupName nvarchar(128), Size numeric(20,0), " +
        "MaxSize numeric(20,0), FileId bigint, CreateLSN numeric(25,0), DropLSN numeric(25,0), UniqueId uniqueidentifier, " +
        "ReadOnlyLSN numeric(25,0), ReadWriteLSN numeric(25,0), BackupSizeInBytes bigint, SourceBlockSize int, FileGroupId int, " +
        "LogGroupGUID uniqueidentifier, DifferentialBaseLSN numeric(25,0), DifferentialBaseGUID uniqueidentifier, IsReadOnly bit, " +
        "IsPresent bit, TDEThumbprint varbinary(32), SnapshotUrl nvarchar(360)";

    private static string SqlServerMoveRestore(string name, string file)
    {
        var restore = $"RESTORE DATABASE {SqlIdentifier(name)} FROM DISK = {SqlString(file)} WITH REPLACE, RECOVERY";
        // Dinamik SQL içindeki dizgeler için tek tırnak iki kez ikilenir: N'''' -> ' ve N'''''' -> ''.
        const string escape = "REPLACE({0}, N'''', N'''''')";
        var logical = string.Format(System.Globalization.CultureInfo.InvariantCulture, escape, "LogicalName");
        var target = string.Format(System.Globalization.CultureInfo.InvariantCulture, escape,
            $"CASE WHEN Type = 'L' THEN @l ELSE @d END + {SqlString(name + "_")} + CAST(FileId AS nvarchar(20)) + " +
            "CASE WHEN Type = 'L' THEN N'.ldf' WHEN FileId = 1 THEN N'.mdf' ELSE N'.ndf' END");

        return
            $"DECLARE @f TABLE ({FileListColumns}); " +
            $"INSERT INTO @f EXEC ({SqlString("RESTORE FILELISTONLY FROM DISK = " + SqlString(file))}); " +
            "DECLARE @d nvarchar(260) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(260)); " +
            "DECLARE @l nvarchar(260) = CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(260)); " +
            "IF RIGHT(@d, 1) <> N'/' SET @d += N'/'; IF RIGHT(@l, 1) <> N'/' SET @l += N'/'; " +
            $"DECLARE @s nvarchar(max) = {SqlString(restore)}; " +
            $"SELECT @s += STRING_AGG(CAST(N', MOVE N''' + {logical} + N''' TO N''' + {target} + N'''' AS nvarchar(max)), N'') FROM @f; " +
            $"IF DB_ID({SqlString(name)}) IS NOT NULL ALTER DATABASE {SqlIdentifier(name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            "EXEC (@s);";
    }

    private static string SqlServerTempFile(string file) =>
        $"mkdir -p {SqlServerBackupDirectory} || exit 2\n" +
        $"SMF={ShellQuote.Quote(file)}\n" +
        "trap 'rm -f \"$SMF\"' EXIT\n" +
        ExitOnSignal;

    /// <summary>BACKUP/RESTORE dosyayı SQL Server'ın diskinde yazdığı/okuduğu için yalnızca yerel bağlantı kullanılır.</summary>
    private static string SqlServerConnection(BackupSourceSpec spec)
    {
        var host = string.IsNullOrWhiteSpace(spec.DatabaseHost) ? "localhost" : spec.DatabaseHost.Trim();
        var server = spec.DatabasePort is { } port ? $"{host},{port}" : host;
        var user = string.IsNullOrWhiteSpace(spec.DatabaseUser) ? BackupDatabaseEngines.DefaultUser(BackupDatabaseEngine.SqlServer)! : spec.DatabaseUser;
        return $"-S {ShellQuote.Quote(server)} -U {ShellQuote.Quote(user)}";
    }

    private static string SqlServerName(string name) =>
        BackupInputPatterns.IsValidDatabaseName(BackupDatabaseEngine.SqlServer, name)
            ? name
            : throw new ArgumentException(BackupInputPatterns.MessageSqlServerDatabaseName, nameof(name));

    /// <summary>T-SQL köşeli parantezli tanımlayıcı; <c>]</c> ikilenir (doğrulama zaten izin vermez, savunma amaçlı).</summary>
    internal static string SqlIdentifier(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";

    /// <summary>T-SQL Unicode dizge; <c>'</c> ikilenir.</summary>
    internal static string SqlString(string value) => "N'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string NewToken() => Guid.NewGuid().ToString("N");

    private static string Token(string token) =>
        token.Length is > 0 and <= 64 && token.All(char.IsAsciiLetterOrDigit)
            ? token
            : throw new ArgumentException("Geçici dosya adı yalnızca harf ve rakam içerebilir.", nameof(token));

    // ---- Ortak -----------------------------------------------------------------------------------------------------

    /// <summary>POSIX sh'da pipefail yok: üreticinin çıkış kodu geçici dosyadan okunur.</summary>
    private static string GzipPipe(string producer) =>
        RequireTool("gzip") +
        "ST=$(mktemp) || exit 2\n" +
        $"{{ {producer}; echo $? > \"$ST\"; }} | gzip -c\n" +
        "GZ=$?\n" +
        "DC=$(cat \"$ST\" 2>/dev/null)\n" +
        "rm -f \"$ST\"\n" +
        "if [ \"${DC:-1}\" != 0 ]; then exit \"${DC:-1}\"; fi\n" +
        "exit \"$GZ\"\n";

    /// <summary>Geri yüklemede gunzip hatası (bozuk dosya) da tüketicinin hatası kadar önemlidir.</summary>
    private static string ImportPipe(string consumer) =>
        "ST=$(mktemp) || exit 2\n" +
        $"{{ gunzip -c; echo $? > \"$ST\"; }} | {consumer}\n" +
        "RC=$?\n" +
        "GC=$(cat \"$ST\" 2>/dev/null)\n" +
        "rm -f \"$ST\"\n" +
        "if [ \"$RC\" != 0 ]; then exit \"$RC\"; fi\n" +
        "exit \"${GC:-1}\"\n";

    /// <summary>
    /// Betiği container içinde (<c>docker exec -i</c>, tek docker çağrısı) veya sunucuda çalıştırır.
    /// Parola stdin'den okunur ve istemcinin beklediği ortam değişkenine aktarılır (MongoDB'de geçici config dosyasına).
    /// </summary>
    private static string Database(BackupSourceSpec spec, string body)
    {
        var passwordExport = spec.Engine switch
        {
            BackupDatabaseEngine.PostgreSql => "export PGPASSWORD=\"$SM_DB_PASSWORD\"\n",
            BackupDatabaseEngine.MySql => "export MYSQL_PWD=\"$SM_DB_PASSWORD\"\n",
            // Boş REDISCLI_AUTH, parolasız sunucuya AUTH gönderip hata alır.
            BackupDatabaseEngine.Redis => "if [ -n \"$SM_DB_PASSWORD\" ]; then export REDISCLI_AUTH=\"$SM_DB_PASSWORD\"; fi\n",
            // Değişken boş da olsa tanımlı olmalı; yoksa sqlcmd parolayı terminalden sorar.
            BackupDatabaseEngine.SqlServer => "export SQLCMDPASSWORD=\"$SM_DB_PASSWORD\"\n",
            _ => string.Empty
        };
        var script = ReadPassword + passwordExport + body;

        return string.IsNullOrWhiteSpace(spec.ContainerName)
            ? Shell(script)
            : $"docker exec -i {ShellQuote.Quote(spec.ContainerName)} sh -c {ShellQuote.Quote(script)}";
    }

    private static string RequiredDatabaseName(BackupSourceSpec spec) =>
        string.IsNullOrEmpty(spec.DatabaseName)
            ? throw new ArgumentException("Veritabanı adı zorunludur.", nameof(spec))
            : spec.DatabaseName;

    /// <summary><paramref name="fallback"/> varsa önce <paramref name="tool"/> (ör. mariadb-dump) denenir.</summary>
    private static string Client(string tool, string? fallback, string arguments)
    {
        var program = fallback is null
            ? $"command -v {tool} >/dev/null 2>&1 || {{ echo '{tool} bulunamadı' >&2; exit {ToolMissingExitCode}; }}; exec {tool} \"$@\""
            : $"if command -v {tool} >/dev/null 2>&1; then exec {tool} \"$@\"; elif command -v {fallback} >/dev/null 2>&1; then exec {fallback} \"$@\"; else echo '{tool}/{fallback} bulunamadı' >&2; exit {ToolMissingExitCode}; fi";

        return $"sh -c {ShellQuote.Quote(program)} sh {arguments}";
    }

    private static string RequireTool(string tool) =>
        $"command -v {tool} >/dev/null 2>&1 || {{ echo '{tool} bulunamadı' >&2; exit {ToolMissingExitCode}; }}\n";

    private static string PostgresConnection(BackupSourceSpec spec) =>
        Connection(spec, "-h", "-p") + $"-U {ShellQuote.Quote(spec.DatabaseUser!)}";

    private static string MySqlConnection(BackupSourceSpec spec) =>
        Connection(spec, "-h", "-P") + $"-u {ShellQuote.Quote(spec.DatabaseUser!)}";

    /// <summary>Container içinde yerel bağlantı kullanılır; sunucuda host/port verilmişse eklenir.</summary>
    private static string Connection(BackupSourceSpec spec, string hostFlag, string portFlag)
    {
        var parts = string.Empty;
        if (!string.IsNullOrWhiteSpace(spec.DatabaseHost))
            parts += $"{hostFlag} {ShellQuote.Quote(spec.DatabaseHost)} ";
        if (spec.DatabasePort is { } port)
            parts += $"{portFlag} {port} ";
        return parts;
    }

    private static string Shell(string script) => "sh -c " + ShellQuote.Quote(script);
}
