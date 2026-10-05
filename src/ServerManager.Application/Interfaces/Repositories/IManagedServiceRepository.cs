using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IManagedServiceRepository
{
    /// <param name="serverId">null ise tüm sunuculardaki servisler.</param>
    Task<IReadOnlyList<ManagedService>> ListAsync(Guid? serverId, CancellationToken cancellationToken = default);

    Task<ManagedService?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(Guid serverId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Silinmiş servisler dahil; kısa ad (ve Docker adları) sunucuda bir kez kullanılır.</summary>
    Task<bool> SlugExistsAsync(Guid serverId, string slug, CancellationToken cancellationToken = default);

    /// <summary>Sunucudaki diğer servislerin kullandığı sunucu portları (silinmemiş servisler).</summary>
    Task<IReadOnlyList<ManagedService>> ListOthersOnServerAsync(Guid serverId, Guid excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(ManagedService service, CancellationToken cancellationToken = default);

    Task<ManagedServiceOperation?> GetOperationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ManagedServiceOperation?> GetRunningOperationAsync(Guid serviceId, CancellationToken cancellationToken = default);

    /// <summary>Log metni olmadan, en yeni önce.</summary>
    Task<IReadOnlyList<ManagedServiceOperation>> ListOperationsAsync(Guid serviceId, int take, CancellationToken cancellationToken = default);

    Task AddOperationAsync(ManagedServiceOperation operation, CancellationToken cancellationToken = default);

    Task UpdateOperationLogAsync(Guid id, string log, CancellationToken cancellationToken = default);

    /// <summary>Önceki çalışmadan yarım kalan işlemleri "kesildi", kurulum/güncelleme durumundaki servisleri "başarısız" yapar.</summary>
    Task<int> InterruptRunningAsync(DateTime finishedAt, string reason, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
