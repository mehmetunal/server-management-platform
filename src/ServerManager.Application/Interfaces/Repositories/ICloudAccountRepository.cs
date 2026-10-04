using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface ICloudAccountRepository
{
    Task<IReadOnlyList<CloudAccount>> GetAllWithServersAsync(CancellationToken cancellationToken = default);

    Task<CloudAccount?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CloudAccount?> GetWithServersAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Hiçbir bulut hesabına bağlı olmayan sunucular (IP eşleştirmesi için).</summary>
    Task<IReadOnlyList<Server>> GetUnlinkedServersAsync(CancellationToken cancellationToken = default);

    Task AddAsync(CloudAccount account, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
