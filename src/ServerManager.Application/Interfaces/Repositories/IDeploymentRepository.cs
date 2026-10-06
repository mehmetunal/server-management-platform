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

    /// <summary>Projeye bağlı (kaldırılmamış) en az bir yönetilen servis var mı; varsa container'lar sm-services ağına katılır.</summary>
    Task<bool> HasServiceLinksAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task AddProjectAsync(DeploymentProject project, CancellationToken cancellationToken = default);

    Task<PagedResult<Deployment>> SearchDeploymentsAsync(DeploymentFilterDto filter, CancellationToken cancellationToken = default);

    Task<Deployment?> GetDeploymentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Deployment?> GetRunningDeploymentAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, Deployment>> GetLatestDeploymentsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default);

    /// <summary>Projede en son başarıyla tamamlanan deployment (sunucuda çalışan sürüm); log yüklenmez.</summary>
    Task<Deployment?> GetLastSuccessfulDeploymentAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Her proje için en son başarılı deployment'ın kimliği.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetCurrentDeploymentIdsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default);

    Task AddDeploymentAsync(Deployment deployment, CancellationToken cancellationToken = default);

    Task UpdateDeploymentLogAsync(Guid id, string log, CancellationToken cancellationToken = default);

    /// <summary>Uygulama kapanırken yarım kalan deployment kayıtlarını silmeden "kesildi" olarak işaretler.</summary>
    Task<int> InterruptRunningDeploymentsAsync(DateTime completedAt, string reason, CancellationToken cancellationToken = default);

    /// <summary>Projenin bekleyen webhook takip deploy'unu yazar (varsa üzerine); izlenen varlığa dokunmaz.</summary>
    Task<bool> SetPendingWebhookDeployAsync(Guid projectId, DateTime queuedAt, string? commit, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Bekleyen webhook takip deploy'ları (silinmemiş projeler, en eski önce).</summary>
    Task<IReadOnlyList<PendingWebhookDeploy>> ListPendingWebhookDeploysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Kuyruk kaydını yalnızca <paramref name="queuedUpTo"/> veya öncesinde yazıldıysa siler; bu arada gelen yeni push kaybolmaz.
    /// </summary>
    Task<bool> ClearPendingWebhookDeployAsync(Guid projectId, DateTime queuedUpTo, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
