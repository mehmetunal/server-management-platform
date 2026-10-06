using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;

namespace ServerManager.Web.Models;

/// <summary>
/// Yedek listelerindeki "İndir" düğmesi. <see cref="DisabledReason"/> doluysa düğme pasif gösterilir ve neden araç ipucunda yazar.
/// </summary>
public sealed record BackupDownloadButtonModel(Guid? RunId, bool IsEncrypted, string? DisabledReason, string Label = "İndir", bool Compact = true)
{
    public static BackupDownloadButtonModel For(BackupRunListItemDto run, string label = "İndir", bool compact = true) =>
        new(run.Id, run.IsEncrypted,
            BackupDownloads.UnavailableReason(run.Operation, run.Status, run.ArtifactDeletedAt, null, hasObjectKey: run.IsArtifactAvailable || run.ArtifactDeletedAt is not null),
            label, compact);

    /// <summary>İşin dosyası duran en yeni yedeği; yoksa pasif düğme.</summary>
    public static BackupDownloadButtonModel ForLatest(BackupJobListItemDto job, string label = "İndir", bool compact = true) =>
        job.LatestBackup is { } latest
            ? new(latest.RunId, latest.IsEncrypted, null, label, compact)
            : new(null, job.EncryptionEnabled, "Bu işin indirilebilir yedeği yok (henüz başarılı yedek alınmadı veya dosyalar silindi).", label, compact);
}
