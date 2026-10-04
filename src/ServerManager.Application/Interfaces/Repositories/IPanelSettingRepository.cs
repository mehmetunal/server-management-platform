using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IPanelSettingRepository
{
    Task<IReadOnlyList<PanelSetting>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(IReadOnlyList<PanelSetting> settings, CancellationToken cancellationToken = default);
}
