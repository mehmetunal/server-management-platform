using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Settings;

namespace ServerManager.Application.Interfaces.Services;

public interface IPanelSettingsService
{
    IReadOnlyList<PanelSettingGroupDto> GetGroups();

    Task ApplyStoredAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult> SaveAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default);
}
