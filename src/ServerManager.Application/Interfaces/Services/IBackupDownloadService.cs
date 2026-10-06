using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;

namespace ServerManager.Application.Interfaces.Services;

public interface IBackupDownloadService
{
    /// <summary>
    /// Başarılı yedeğin dosyasını depolamadan akış olarak açar (belleğe alınmaz). <paramref name="passphrase"/> boşsa dosya
    /// olduğu gibi (şifreliyse .smbk) verilir; şifreli yedekte parola girilmişse önce doğrulanır, sonra akış hâlinde çözülür.
    /// İndirme audit log'a yazılır. Dönen akışı çağıran kapatır.
    /// </summary>
    Task<ServiceResult<BackupDownload>> OpenAsync(Guid runId, string? passphrase, BackupActor actor, CancellationToken cancellationToken = default);
}
