using ServerManager.Application.Common;
using ServerManager.Plugin.DevOps.Dokku.DTOs;

namespace ServerManager.Plugin.DevOps.Dokku.Services;

public interface IDokkuService
{
    Task<ServiceResult<DokkuOverviewDto>> GetOverviewAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult> StartInstallAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult> RestartAsync(Guid serverId, string app, CancellationToken cancellationToken = default);
}
