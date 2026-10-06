using ServerManager.Application.Cleanup;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.ServerSystem;

/// <summary>Temizlik sayfası: sunucuda silinebilecekleri tarar ve seçilen öğeleri siler (sudo ayarlıysa sudo ile).</summary>
public interface IServerCleanupInspector
{
    Task<ServiceResult<CleanupFacts>> ScanAsync(RemoteExecutionContext context, CleanupOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Öğeleri sırayla işler ve her adımı <paramref name="onLog"/>'a bildirir. Önizlemede (dryRun) hiçbir şey silinmez:
    /// destekleyen araçlar simülasyon modunda çalışır, diğerleri için çalıştırılacak komut gösterilir.
    /// </summary>
    Task<ServiceResult<CleanupRunResult>> ExecuteAsync(
        RemoteExecutionContext context,
        IReadOnlyList<CleanupItem> items,
        CleanupOptions options,
        bool dryRun,
        Func<CleanupLogEntry, CancellationToken, Task> onLog,
        CancellationToken cancellationToken = default);
}
