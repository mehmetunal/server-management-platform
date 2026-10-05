using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

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

    /// <summary>
    /// Proje formundaki sunucu veya tür değişikliğinin domainlerle uyumlu olduğunu doğrular: Docker Compose'a geçerken her
    /// domainin servis adı olmalı, başka sunucuya taşırken host ve yol orada boş olmalıdır.
    /// </summary>
    Task<ServiceResult> ValidateProjectChangeAsync(DeploymentProject project, Guid serverId, DeploymentBuildType buildType, CancellationToken cancellationToken = default);

    /// <summary>Proje başka sunucuya taşındıktan sonra domain kayıtlarını yeni sunucuya bağlar.</summary>
    Task OnProjectServerChangedAsync(DeploymentProject project, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DeploymentRoute>>> GetRoutesAsync(DeploymentProject project, CancellationToken cancellationToken = default);
}
