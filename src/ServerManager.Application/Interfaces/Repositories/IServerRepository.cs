using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IServerRepository : IRepository<Server>
{
    Task<Server?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Server>> SearchAsync(ServerFilterDto filter, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<ServerStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Server>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetTagNamesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetMonitorableIdsAsync(CancellationToken cancellationToken = default);
}
