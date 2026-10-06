using ServerManager.Application.Common;
using ServerManager.Application.ResourceUsage;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>
/// "Dün gece ne yavaşlattı?": container ve process kullanımının periyodik örnekleri, saatlik özetleri ve sorgulanması.
/// Ayrıca "Temizlenebilir alan" alarmı için düşük sıklıkta temizlik taraması (silmeden) yapar.
/// </summary>
public interface IResourceHistoryService
{
    bool Enabled { get; }

    int IntervalMinutes { get; }

    Task<IReadOnlyList<Guid>> GetCollectableServerIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Sunucudan bir örnek alır ve saklar; dönen değer saklanan container örneği sayısıdır.</summary>
    Task<ServiceResult<int>> CollectAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<int> AggregateHourlyAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<ResourceHistoryReport>> GetReportAsync(
        Guid serverId, string? range, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProcessSnapshotView>> GetProcessSnapshotAsync(Guid serverId, DateTime atUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetReclaimableScanDueServerIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Temizlik taraması (silme yok) yapar ve geri kazanılabilir alanı kaydeder.</summary>
    Task<ServiceResult<long>> ScanReclaimableAsync(Guid serverId, CancellationToken cancellationToken = default);
}
