using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Uptime;

namespace ServerManager.Application.Interfaces.Services;

public interface IUptimeService
{
    Task<PagedResult<UptimeCheckListItemDto>> SearchAsync(UptimeFilterDto filter, CancellationToken cancellationToken = default);

    Task<ServiceResult<UptimeCheckDetailsDto>> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<UptimeCheckFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(UptimeCheckFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(UptimeCheckFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<UptimeProbeResult>> CheckNowAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetDueCheckIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Arka plan işi için tek bir kontrolü çalıştırır ve sonucu kaydeder.</summary>
    Task RunCheckAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> RunMaintenanceAsync(CancellationToken cancellationToken = default);
}
