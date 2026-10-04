using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IServerTemplateRepository
{
    Task<IReadOnlyList<ServerTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ServerTemplate?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(ServerTemplate template, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
