using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Backups;

/// <summary>
/// Sunucuda çalışan yedekleme/geri yükleme komutları. Çıktı gzip ile sıkıştırılır. Veritabanı parolası stdin'in ilk
/// satırından okunur, komut satırına yazılmaz. Volume ve container veritabanı komutları tek bir <c>docker</c> çağrısıdır;
/// böylece yalnızca docker grubu veya yalnızca docker için sudo yetkisi olan kullanıcılarla da çalışır.
/// Hiçbir komut dosya silmez; geri yükleme yalnızca üzerine yazar.
/// </summary>
public static class BackupCommands
{
    public const int SourceMissingExitCode = 3;
    public const int ToolMissingExitCode = 4;

    /// <summary>Volume içeriğini arşivleyen yardımcı container imajı (BusyBox tar içerir).</summary>
    public const string VolumeHelperImage = "alpine:3";

    private const string ReadPassword = "IFS= read -r SM_DB_PASSWORD || true\n";

    /// <summary>GNU tar'a özgü seçenekler yalnızca GNU tar varsa eklenir; BusyBox (Alpine) tar da desteklenir.</summary>
    private const string DetectTar = "TW=\nif tar --version 2>/dev/null | grep -q GNU; then TW=--warning=no-file-changed; fi\n";

    /// <summary>GNU tar 1 ile çıkarsa (okunurken değişen dosyalar) yedek yine geçerlidir; BusyBox'ta 1 hatadır.</summary>
    private const string TarResult = "rc=$?\nif [ \"$rc\" -eq 1 ] && [ -n \"$TW\" ]; then exit 0; fi\nexit \"$rc\"\n";

    /// <summary>Container çıktısı Docker log dosyasına yazılmaz (büyük yedekler diski doldurmasın).</summary>
    private const string HelperRunOptions = "--rm --network none --log-driver none";

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

    private static string ExportDatabase(BackupSourceSpec spec)
    {
        var dump = spec.Engine == BackupDatabaseEngine.MySql
            ? Client("mariadb-dump", "mysqldump",
                $"--single-transaction --routines --triggers --no-tablespaces {MySqlConnection(spec)} {ShellQuote.Quote(spec.DatabaseName!)}")
            : Client("pg_dump", null,
                $"--clean --if-exists --no-owner --no-privileges {PostgresConnection(spec)} -d {ShellQuote.Quote(spec.DatabaseName!)}");

        // POSIX sh'da pipefail yok: dökümün çıkış kodu geçici dosyadan okunur.
        return Database(spec,
            RequireTool("gzip") +
            "ST=$(mktemp) || exit 2\n" +
            $"{{ {dump}; echo $? > \"$ST\"; }} | gzip -c\n" +
            "GZ=$?\n" +
            "DC=$(cat \"$ST\" 2>/dev/null)\n" +
            "rm -f \"$ST\"\n" +
            "if [ \"${DC:-1}\" != 0 ]; then exit \"${DC:-1}\"; fi\n" +
            "exit \"$GZ\"\n");
    }

    private static string ImportDatabase(BackupSourceSpec spec)
    {
        var apply = spec.Engine == BackupDatabaseEngine.MySql
            ? Client("mariadb", "mysql", $"{MySqlConnection(spec)} {ShellQuote.Quote(spec.DatabaseName!)}")
            : Client("psql", null, $"-q -v ON_ERROR_STOP=1 {PostgresConnection(spec)} -d {ShellQuote.Quote(spec.DatabaseName!)}");

        return Database(spec, RequireTool("gunzip") + $"gunzip -c | {apply}\n");
    }

    /// <summary>
    /// Betiği container içinde (<c>docker exec -i</c>, tek docker çağrısı) veya sunucuda çalıştırır.
    /// Parola stdin'den okunur ve istemcinin beklediği ortam değişkenine aktarılır.
    /// </summary>
    private static string Database(BackupSourceSpec spec, string body)
    {
        var passwordVariable = spec.Engine == BackupDatabaseEngine.MySql ? "MYSQL_PWD" : "PGPASSWORD";
        var script = ReadPassword + $"export {passwordVariable}=\"$SM_DB_PASSWORD\"\n" + body;

        return string.IsNullOrWhiteSpace(spec.ContainerName)
            ? Shell(script)
            : $"docker exec -i {ShellQuote.Quote(spec.ContainerName)} sh -c {ShellQuote.Quote(script)}";
    }

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
