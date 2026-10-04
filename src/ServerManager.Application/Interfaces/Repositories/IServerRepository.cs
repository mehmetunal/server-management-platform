using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IServerRepository : IRepository<Server>
{
    Task<Server?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Yumuşak silinmiş sunucunun adı. Kayıt yoksa veya hâlâ aktifse null.</summary>
    Task<string?> GetRemovedNameAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Server>> SearchAsync(ServerFilterDto filter, CancellationToken cancellationToken = default);

    Task<bool> GroupExistsAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<Server?> GetByAgentTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Server>> GetSilentAgentServersAsync(DateTime lastSeenBefore, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Server>> GetAllForCostReportAsync(CancellationToken cancellationToken = default);

    Task<bool> CloudAccountExistsAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<bool> CloudLinkExistsAsync(Guid accountId, string externalId, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<ServerStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Server>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetTagNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>İzlemesi açık, host key'i doğrulanmış ve agent'ı <paramref name="agentActiveSince"/> sonrasında rapor göndermemiş sunucular.</summary>
    Task<IReadOnlyList<Guid>> GetMonitorableIdsAsync(DateTime agentActiveSince, CancellationToken cancellationToken = default);
}
