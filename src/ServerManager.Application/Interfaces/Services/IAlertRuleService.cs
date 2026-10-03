using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Application.Interfaces.Services;

public interface IAlertRuleService
{
    Task<IReadOnlyList<AlertRuleListItemDto>> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<AlertRuleFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(AlertRuleFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(AlertRuleFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Alarm, uptime ve SSL formlarındaki sunucu seçimi için.</summary>
    Task<IReadOnlyList<ServerOptionDto>> GetServerOptionsAsync(CancellationToken cancellationToken = default);
}
