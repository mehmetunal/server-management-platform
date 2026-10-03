using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Uptime;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IUptimeRepository
{
    Task<PagedResult<UptimeCheck>> SearchAsync(UptimeFilterDto filter, CancellationToken cancellationToken = default);

    Task<UptimeCheck?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(UptimeCheck check, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTime now, int limit, CancellationToken cancellationToken = default);

    Task AddResultAsync(UptimeCheckResult result, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UptimeCheckResult>> GetRecentResultsAsync(Guid checkId, int count, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, double>> GetUptimePercentAsync(IReadOnlyCollection<Guid> checkIds, DateTime since, CancellationToken cancellationToken = default);

    Task<double?> GetAverageResponseMsAsync(Guid checkId, DateTime since, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredResultsAsync(DateTime cutoffUtc, int keepLatestPerCheck, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
