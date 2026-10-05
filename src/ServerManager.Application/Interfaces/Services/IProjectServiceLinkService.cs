using ServerManager.Application.Common;
using ServerManager.Application.DTOs.ManagedServices;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>
/// "Projeye bağla": yönetilen servisin bağlantı bilgisini aynı sunucudaki deployment projesinin ortam değişkenlerine yazar ve
/// bağı kaydeder. Bağlı projenin container'ları bir sonraki deploy / yeniden başlatmada <c>sm-services</c> ağına katılır.
/// Yetki kontrolü çağıran katmandadır (services.manage + deployment.manage).
/// </summary>
public interface IProjectServiceLinkService
{
    Task<ServiceResult<ServiceLinkPreviewDto>> GetPreviewAsync(Guid serviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectServiceLinkDto>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectServiceLinkDto>> ListForServiceAsync(Guid serviceId, CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceLinkResultDto>> LinkAsync(Guid serviceId, LinkServiceToProjectDto dto, CancellationToken cancellationToken = default);

    /// <summary>Bağı kaldırır. <paramref name="removeKeys"/> true ise bağlarken eklenen ortam değişkenleri de silinir.</summary>
    Task<ServiceResult> UnlinkAsync(Guid linkId, bool removeKeys, CancellationToken cancellationToken = default);
}
