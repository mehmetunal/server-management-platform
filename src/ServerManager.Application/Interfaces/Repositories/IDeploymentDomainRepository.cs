using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IDeploymentDomainRepository
{
    Task<IReadOnlyList<DeploymentDomain>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<DeploymentDomain?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<bool> HostPathExistsAsync(Guid serverId, string host, string path, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(DeploymentDomain domain, CancellationToken cancellationToken = default);

    Task SoftDeleteByProjectAsync(Guid projectId, string? deletedBy, DateTime deletedAt, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
