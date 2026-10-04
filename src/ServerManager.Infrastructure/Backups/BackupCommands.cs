using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Backups;

/// <summary>
/// Sunucuda çalışan yedekleme/geri yükleme betikleri (POSIX sh). Çıktı gzip ile sıkıştırılır.
/// Veritabanı parolası stdin'in ilk satırından okunur, komut satırına yazılmaz; container'a <c>docker exec -e AD</c>
/// ile değeri olmadan (ortamdan) geçirilir. Hiçbir betik dosya silmez; geri yükleme yalnızca üzerine yazar.
/// </summary>
public static class BackupCommands
{
    public const int SourceMissingExitCode = 3;
    public const int ToolMissingExitCode = 4;

    private const string ReadPassword = "IFS= read -r SM_DB_PASSWORD || true\n";

    /// <summary>GNU tar'a özgü seçenekler yalnızca GNU tar varsa eklenir; BusyBox (Alpine) tar da desteklenir.</summary>
    private const string DetectTar = "TW=\nif tar --version 2>/dev/null | grep -q GNU; then TW=--warning=no-file-changed; fi\n";

    /// <summary>GNU tar 1 ile çıkarsa (okunurken değişen dosyalar) yedek yine geçerlidir; BusyBox'ta 1 hatadır.</summary>
    private const string TarResult = "rc=$?\nif [ \"$rc\" -eq 1 ] && [ -n \"$TW\" ]; then exit 0; fi\nexit \"$rc\"\n";

    public static bool NeedsPasswordInput(BackupSourceSpec spec) => spec.Type == BackupSourceType.Database;

    public static string PasswordInput(BackupSourceSpec spec) => (spec.DatabasePassword ?? string.Empty) + "\n";

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

    private static string ExportVolume(BackupSourceSpec spec) =>
        Shell(
            VolumeMountpoint(spec.VolumeName!, create: false) +
            DetectTar +
            "cd \"$M\" || exit 2\n" +
            "tar -czf - $TW .\n" +
            TarResult);

    private static string ExportDatabase(BackupSourceSpec spec)
    {
        var dump = spec.Engine == BackupDatabaseEngine.MySql
            ? Client(spec, "MYSQL_PWD", "mariadb-dump", "mysqldump",
                $"--single-transaction --routines --triggers --no-tablespaces {MySqlConnection(spec)} {ShellQuote.Quote(spec.DatabaseName!)}")
            : Client(spec, "PGPASSWORD", "pg_dump", null,
                $"--clean --if-exists --no-owner --no-privileges {PostgresConnection(spec)} -d {ShellQuote.Quote(spec.DatabaseName!)}");

        // POSIX sh'da pipefail yok: dökümün çıkış kodu geçici dosyadan okunur.
        return Shell(
            ReadPassword +
            "ST=$(mktemp) || exit 2\n" +
            $"{{ {dump}; echo $? > \"$ST\"; }} | gzip -c\n" +
            "GZ=$?\n" +
            "DC=$(cat \"$ST\" 2>/dev/null)\n" +
            "rm -f \"$ST\"\n" +
            "if [ \"${DC:-1}\" != 0 ]; then exit \"${DC:-1}\"; fi\n" +
            "exit \"$GZ\"\n");
    }

    private static string ImportFiles(BackupSourceSpec spec)
    {
        var target = ShellQuote.Quote(spec.TargetDirectory ?? "/");
        return Shell(
            $"mkdir -p -- {target} || exit 2\n" +
            $"tar -xzpf - -C {target}\n");
    }

    private static string ImportVolume(BackupSourceSpec spec) =>
        Shell(
            VolumeMountpoint(spec.VolumeName!, create: true) +
            "tar -xzpf - -C \"$M\"\n");

    private static string ImportDatabase(BackupSourceSpec spec)
    {
        var apply = spec.Engine == BackupDatabaseEngine.MySql
            ? Client(spec, "MYSQL_PWD", "mariadb", "mysql", $"{MySqlConnection(spec)} {ShellQuote.Quote(spec.DatabaseName!)}", interactive: true)
            : Client(spec, "PGPASSWORD", "psql", null,
                $"-q -v ON_ERROR_STOP=1 {PostgresConnection(spec)} -d {ShellQuote.Quote(spec.DatabaseName!)}", interactive: true);

        return Shell(
            ReadPassword +
            $"gunzip -c | {apply}\n");
    }

    private static string VolumeMountpoint(string volume, bool create)
    {
        var name = ShellQuote.Quote(volume);
        var ensure = create
            ? $"docker volume inspect {name} >/dev/null 2>&1 || docker volume create {name} >/dev/null || exit 2\n"
            : $"docker volume inspect {name} >/dev/null 2>&1 || {{ echo {ShellQuote.Quote("Volume bulunamadı: " + volume)} >&2; exit {SourceMissingExitCode}; }}\n";
        return ensure +
               $"M=$(docker volume inspect --format '{{{{.Mountpoint}}}}' {name}) || exit 2\n" +
               $"if [ ! -d \"$M\" ]; then echo {ShellQuote.Quote("Volume klasörüne erişilemiyor; sunucuda sudo (root) yetkisi gerekir, rootless Docker ve Docker Desktop desteklenmez: ")}\"$M\" >&2; exit 2; fi\n";
    }

    /// <summary>
    /// İstemciyi container'da (<c>docker exec</c>) veya sunucuda çalıştırır. Parola ortam değişkeniyle verilir.
    /// <paramref name="fallback"/> varsa önce <paramref name="tool"/> (ör. mariadb-dump) denenir.
    /// </summary>
    private static string Client(BackupSourceSpec spec, string passwordVariable, string tool, string? fallback, string arguments, bool interactive = false)
    {
        var program = fallback is null
            ? $"command -v {tool} >/dev/null 2>&1 || {{ echo '{tool} bulunamadı' >&2; exit {ToolMissingExitCode}; }}; exec {tool} \"$@\""
            : $"if command -v {tool} >/dev/null 2>&1; then exec {tool} \"$@\"; elif command -v {fallback} >/dev/null 2>&1; then exec {fallback} \"$@\"; else echo '{tool}/{fallback} bulunamadı' >&2; exit {ToolMissingExitCode}; fi";

        var environment = $"{passwordVariable}=\"$SM_DB_PASSWORD\"";
        if (string.IsNullOrWhiteSpace(spec.ContainerName))
            return $"{environment} sh -c {ShellQuote.Quote(program)} sh {arguments}";

        var flags = interactive ? "-i " : string.Empty;
        return $"{environment} docker exec {flags}-e {passwordVariable} {ShellQuote.Quote(spec.ContainerName)} sh -c {ShellQuote.Quote(program)} sh {arguments}";
    }

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
