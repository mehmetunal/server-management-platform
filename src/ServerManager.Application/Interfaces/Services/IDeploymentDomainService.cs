using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Services;

public interface IDeploymentDomainService
{
    Task<IReadOnlyList<DomainListItemDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProxyStatusDto>> GetProxyStatusAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ServiceResult> InstallProxyAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ServiceResult> SaveAsync(Guid projectId, DomainFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid projectId, Guid domainId, string? confirmationHost, CancellationToken cancellationToken = default);

    Task<ServiceResult> ApplyAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Proje silinince domain kayıtlarını kapatır.
    /// <paramref name="detachRouting"/> ise sunucudaki yönlendirmeyi kaldırmayı dener; kalıcı silmede dosyalar zaten kalkmıştır.
    /// </summary>
    Task OnProjectDeletedAsync(DeploymentProject project, CancellationToken cancellationToken = default, bool detachRouting = true);

    Task<ServiceResult<IReadOnlyList<DeploymentRoute>>> GetRoutesAsync(DeploymentProject project, CancellationToken cancellationToken = default);
}
