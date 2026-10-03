using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IDeploymentRepository
{
    Task<PagedResult<DeploymentProject>> SearchProjectsAsync(ProjectFilterDto filter, CancellationToken cancellationToken = default);

    Task<DeploymentProject?> GetProjectAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeploymentProject>> GetProjectsByServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<bool> ProjectExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ProjectNameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Silinmiş projeler dahil; slug Docker kaynak adlarında kullanıldığı için tekrar kullanılmaz.</summary>
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);

    Task AddProjectAsync(DeploymentProject project, CancellationToken cancellationToken = default);

    Task<PagedResult<Deployment>> SearchDeploymentsAsync(DeploymentFilterDto filter, CancellationToken cancellationToken = default);

    Task<Deployment?> GetDeploymentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Deployment?> GetRunningDeploymentAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, Deployment>> GetLatestDeploymentsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default);

    Task AddDeploymentAsync(Deployment deployment, CancellationToken cancellationToken = default);

    Task UpdateDeploymentLogAsync(Guid id, string log, CancellationToken cancellationToken = default);

    /// <summary>Uygulama kapanırken yarım kalan deployment kayıtlarını silmeden "kesildi" olarak işaretler.</summary>
    Task<int> InterruptRunningDeploymentsAsync(DateTime completedAt, string reason, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
