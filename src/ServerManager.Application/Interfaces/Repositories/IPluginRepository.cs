using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IPluginRepository
{
    Task<IReadOnlyList<InstalledPlugin>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<InstalledPlugin?> GetAsync(string systemName, CancellationToken cancellationToken = default);

    Task AddAsync(InstalledPlugin plugin, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
