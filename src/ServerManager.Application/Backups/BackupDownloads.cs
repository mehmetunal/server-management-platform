using System.Text.RegularExpressions;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Backups;

/// <summary>Yedek indirme kuralları: hangi çalışmanın dosyası indirilebilir ve indirilen dosyanın adı.</summary>
public static partial class BackupDownloads
{
    public const int MaxFileNameLength = 200;

    /// <summary>İndirme yapılamıyorsa kullanıcıya gösterilecek neden; indirilebiliyorsa null.</summary>
    public static string? UnavailableReason(BackupOperation operation, BackupRunStatus status, DateTime? artifactDeletedAt, string? artifactDeletedBy, bool hasObjectKey)
    {
        if (operation != BackupOperation.Backup)
            return "Geri yükleme kaydının indirilecek dosyası yoktur; kaynak yedeği indirin.";
        if (status == BackupRunStatus.Running)
            return "Yedekleme sürüyor; tamamlanınca indirilebilir.";
        if (status != BackupRunStatus.Succeeded)
            return "Yalnızca başarılı yedekler indirilebilir.";
        if (artifactDeletedAt is not null)
            return string.IsNullOrEmpty(artifactDeletedBy)
                ? "Yedek dosyası depolamadan silinmiş."
                : $"Yedek dosyası depolamadan silinmiş ({artifactDeletedBy}).";
        if (!hasObjectKey)
            return "Bu yedeğin dosya kaydı yok.";
        return null;
    }

    public static string? UnavailableReason(BackupRun run) =>
        UnavailableReason(run.Operation, run.Status, run.ArtifactDeletedAt, run.ArtifactDeletedBy, !string.IsNullOrEmpty(run.ObjectKey));

    /// <summary>
    /// Content-Disposition'a yazılacak güvenli ad: kayıtlı dosya adı, yoksa nesne adının son parçası. Yalnızca harf, rakam,
    /// nokta, tire ve alt çizgi kalır; yol ayırıcı, tırnak ve kontrol karakteri geçemez. Çok uzunsa uzantı korunarak baştan kısaltılır.
    /// </summary>
    public static string SafeFileName(string? fileName, string? objectKey, Guid runId)
    {
        var candidate = !string.IsNullOrWhiteSpace(fileName)
            ? fileName
            : objectKey?[(objectKey.LastIndexOf('/') + 1)..];

        var safe = UnsafeCharacters().Replace(candidate ?? string.Empty, "-");
        if (safe.Length > MaxFileNameLength)
            safe = safe[^MaxFileNameLength..];
        safe = safe.TrimStart('.', '-');

        return safe.Length == 0 || safe.All(c => c is '.' or '-' or '_')
            ? $"yedek-{runId.ToString("N")[..8]}.bin"
            : safe;
    }

    public static string ContentType(string fileName) =>
        fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? "application/gzip" : "application/octet-stream";

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeCharacters();
}
