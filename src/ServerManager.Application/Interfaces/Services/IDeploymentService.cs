using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Deployments;

namespace ServerManager.Application.Interfaces.Services;

public interface IDeploymentService
{
    Task<PagedResult<DeploymentListItemDto>> SearchAsync(DeploymentFilterDto filter, CancellationToken cancellationToken = default);

    Task<ServiceResult<DeploymentDetailsDto>> GetAsync(Guid id, bool includeLog, CancellationToken cancellationToken = default);

    /// <summary>Kaydı oluşturur ve başlatma kontrollerini yapar; adımları <see cref="RunAsync"/> çalıştırır.</summary>
    Task<ServiceResult<Guid>> BeginAsync(Guid projectId, StartDeploymentDto dto, DeploymentActor actor, CancellationToken cancellationToken = default);

    /// <summary>Daha önceki bir deployment'ın commit'ini yeniden dağıtmak için yeni kayıt açar.</summary>
    Task<ServiceResult<Guid>> BeginRedeployAsync(Guid deploymentId, DeploymentActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Önceki başarılı bir deployment'ın sürümüne geri dönmek için yeni kayıt (<c>Kind = Rollback</c>) açar. Dockerfile
    /// projesinde o commit'in imajı sunucuda duruyorsa build yapılmaz; yoksa commit yeniden çekilip build edilir.
    /// </summary>
    Task<ServiceResult<Guid>> BeginRollbackAsync(Guid deploymentId, DeploymentActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ortam değişkenlerini uygulamak için build etmeden yeniden başlatma kaydı (<c>Kind = Restart</c>) açar; çalışan
    /// sürüm (son başarılı deployment) değişmez.
    /// </summary>
    Task<ServiceResult<Guid>> BeginRestartAsync(Guid projectId, DeploymentActor actor, CancellationToken cancellationToken = default);

    Task<ServiceResult> RunAsync(Guid deploymentId, DeploymentActor actor, IDeploymentObserver observer, DeploymentCancellation cancellation);

    Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default);
}
