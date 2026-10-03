using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssl;

namespace ServerManager.Application.Interfaces.Services;

public interface ISslCertificateService
{
    Task<PagedResult<SslMonitorListItemDto>> SearchAsync(SslMonitorFilterDto filter, CancellationToken cancellationToken = default);

    Task<ServiceResult<SslMonitorFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(SslMonitorFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(SslMonitorFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<SslMonitorListItemDto>> CheckNowAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetDueIdsAsync(CancellationToken cancellationToken = default);

    Task RunCheckAsync(Guid id, CancellationToken cancellationToken = default);
}
