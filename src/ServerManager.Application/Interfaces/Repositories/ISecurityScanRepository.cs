using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface ISecurityScanRepository
{
    Task AddAsync(SecurityScan scan, CancellationToken cancellationToken = default);

    /// <summary>Takip edilen kayıt (rapor dahil).</summary>
    Task<SecurityScan?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Sunucunun son tamamlanan taraması (rapor dahil).</summary>
    Task<SecurityScan?> GetLatestCompletedAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>Rapor içermeyen geçmiş, yeniden eskiye.</summary>
    Task<IReadOnlyList<SecurityScan>> GetHistoryAsync(Guid serverId, int take, CancellationToken cancellationToken = default);

    /// <summary>Silinmemiş sunucuların son taramaları (durumdan bağımsız, rapor içermez).</summary>
    Task<IReadOnlyDictionary<Guid, SecurityScan>> GetLatestPerServerAsync(CancellationToken cancellationToken = default);

    /// <summary>Silinmemiş sunucuların son tamamlanan taramaları (rapor içermez); alarm değerlendirmesi için.</summary>
    Task<IReadOnlyList<SecurityScan>> GetLatestCompletedPerServerAsync(CancellationToken cancellationToken = default);

    Task<bool> HasRunningAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<int> InterruptRunningAsync(DateTime completedAt, string reason, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredAsync(DateTime cutoffUtc, int keepLatestPerServer, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
