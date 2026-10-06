using ServerManager.Application.ResourceUsage;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

/// <summary>Container örnekleri, saatlik özetler, process anlık görüntüleri ve temizlenebilir alan özeti.</summary>
public interface IResourceHistoryRepository
{
    /// <summary>Kaynak geçmişi toplanacak sunucular: izleme açık, host key doğrulanmış ve çevrimdışı olmayan.</summary>
    Task<IReadOnlyList<Guid>> GetCollectableServerIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Sunucudaki (kaldırılmamış) yönetilen servislerin container adları.</summary>
    Task<IReadOnlyList<string>> GetManagedContainerNamesAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task AddSamplesAsync(IReadOnlyList<ContainerMetricSample> samples, CancellationToken cancellationToken = default);

    Task AddProcessSnapshotAsync(ProcessSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Verilen saatten önceki ham örnekleri saatlik özete yazar (zaten özetlenen saatler atlanır).</summary>
    Task<int> AggregateHourlyAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);

    /// <summary>Kovalara göre container başına ortalama CPU (%) ve bellek (bayt).</summary>
    Task<IReadOnlyList<ContainerSeriesPoint>> GetSeriesAsync(
        Guid serverId, DateTime fromUtc, DateTime toUtc, int bucketSeconds, bool hourly, CancellationToken cancellationToken = default);

    /// <summary>Aralıkta container başına özet (ağ / disk G/Ç ve yeniden başlama artışı ham örneklerden).</summary>
    Task<IReadOnlyList<ContainerUsageSummary>> GetContainerSummariesAsync(
        Guid serverId, DateTime fromUtc, DateTime toUtc, bool hourly, CancellationToken cancellationToken = default);

    /// <summary>Aralıktaki process anlık görüntüleri (eşit aralıklı en fazla <paramref name="max"/> tane).</summary>
    Task<IReadOnlyList<ProcessSnapshotRow>> GetProcessSnapshotsAsync(
        Guid serverId, DateTime fromUtc, DateTime toUtc, int max, CancellationToken cancellationToken = default);

    /// <summary>Verilen ana en yakın process anlık görüntüsü.</summary>
    Task<ProcessSnapshotRow?> GetNearestProcessSnapshotAsync(Guid serverId, DateTime atUtc, CancellationToken cancellationToken = default);

    Task UpsertReclaimableAsync(ServerReclaimableSpace value, CancellationToken cancellationToken = default);

    /// <summary>Temizlenebilir alan taraması zamanı gelen sunucular (hiç taranmamış veya son tarama <paramref name="scannedBefore"/>'dan eski).</summary>
    Task<IReadOnlyList<Guid>> GetReclaimableScanDueServerIdsAsync(DateTime scannedBefore, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
