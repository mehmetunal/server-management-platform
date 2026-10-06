using ServerManager.Application.Cleanup;
using ServerManager.Application.Common;

namespace ServerManager.Application.Interfaces.Services;

public interface IServerCleanupService
{
    Task<ServiceResult<CleanupScan>> ScanAsync(Guid serverId, CleanupOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sunucu yeniden taranır; yalnızca taramada hâlâ bulunan ve silinebilir olan anahtarlar işlenir. Gerçek çalıştırma audit'e yazılır.
    /// </summary>
    Task<ServiceResult<CleanupRunResult>> ExecuteAsync(
        Guid serverId,
        IReadOnlyCollection<string> keys,
        CleanupOptions options,
        bool dryRun,
        Func<CleanupLogEntry, CancellationToken, Task> onLog,
        CancellationToken cancellationToken = default);
}
