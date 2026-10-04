using ServerManager.Application.DTOs.Settings;

namespace ServerManager.Application.Interfaces.Services;

public interface ISystemInfoService
{
    Task<SystemInfoDto> GetAsync(CancellationToken cancellationToken = default);
}
