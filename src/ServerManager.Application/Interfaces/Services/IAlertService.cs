using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;

namespace ServerManager.Application.Interfaces.Services;

public interface IAlertService
{
    Task<PagedResult<AlertEventDto>> SearchAsync(AlertEventFilterDto filter, CancellationToken cancellationToken = default);

    Task<AlertSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tüm etkin kuralları değerlendirir, alarmları açar/kapatır ve bildirimleri gönderir (arka plan işi).</summary>
    Task<AlertEvaluationResult> EvaluateAsync(CancellationToken cancellationToken = default);

    Task<int> RunMaintenanceAsync(CancellationToken cancellationToken = default);
}
