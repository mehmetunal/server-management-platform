using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IServerGroupRepository
{
    Task<IReadOnlyList<ServerGroup>> GetAllWithServersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServerGroup>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ServerGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Server>> GetServersAsync(IReadOnlyCollection<Guid> serverIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Server>> GetGroupServersAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task AddAsync(ServerGroup group, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
