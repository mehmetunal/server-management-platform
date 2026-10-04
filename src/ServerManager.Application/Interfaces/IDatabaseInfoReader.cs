using ServerManager.Application.DTOs.Settings;

namespace ServerManager.Application.Interfaces;

public interface IDatabaseInfoReader
{
    Task<DatabaseStatusDto> GetAsync(CancellationToken cancellationToken = default);
}
