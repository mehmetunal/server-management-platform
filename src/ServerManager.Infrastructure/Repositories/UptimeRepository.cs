using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Uptime;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class UptimeRepository : IUptimeRepository
{
    private readonly ApplicationDbContext _context;

    public UptimeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<UptimeCheck>> SearchAsync(UptimeFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);

        var query = _context.UptimeChecks.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(u =>
                u.Name.Contains(search)
                || (u.Url != null && u.Url.Contains(search))
                || (u.Host != null && u.Host.Contains(search))
                || (u.Server != null && u.Server.Name.Contains(search)));
        }

        if (filter.Status.HasValue)
            query = query.Where(u => u.Status == filter.Status.Value);

        if (filter.ServerId.HasValue)
            query = query.Where(u => u.ServerId == filter.ServerId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(u => u.Server)
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UptimeCheck>(items, totalCount, page, pageSize);
    }

    public Task<UptimeCheck?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.UptimeChecks
            .Include(u => u.Server)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.UptimeChecks.AnyAsync(u => u.Name == name && (excludeId == null || u.Id != excludeId), cancellationToken);

    public async Task AddAsync(UptimeCheck check, CancellationToken cancellationToken = default) =>
        await _context.UptimeChecks.AddAsync(check, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTime now, int limit, CancellationToken cancellationToken = default) =>
        await _context.UptimeChecks
            .AsNoTracking()
            .Where(u => u.IsEnabled && (u.LastCheckedAt == null || u.LastCheckedAt.Value.AddSeconds(u.IntervalSeconds) <= now))
            .OrderBy(u => u.LastCheckedAt)
            .Take(limit)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

    public async Task AddResultAsync(UptimeCheckResult result, CancellationToken cancellationToken = default) =>
        await _context.UptimeCheckResults.AddAsync(result, cancellationToken);

    public async Task<IReadOnlyList<UptimeCheckResult>> GetRecentResultsAsync(Guid checkId, int count, CancellationToken cancellationToken = default) =>
        await _context.UptimeCheckResults
            .AsNoTracking()
            .Where(r => r.CheckId == checkId)
            .OrderByDescending(r => r.CheckedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, double>> GetUptimePercentAsync(IReadOnlyCollection<Guid> checkIds, DateTime since, CancellationToken cancellationToken = default)
    {
        if (checkIds.Count == 0)
            return new Dictionary<Guid, double>();

        var rows = await _context.UptimeCheckResults
            .Where(r => checkIds.Contains(r.CheckId) && r.CheckedAt >= since)
            .GroupBy(r => r.CheckId)
            .Select(g => new { g.Key, Total = g.Count(), Up = g.Count(r => r.IsUp) })
            .ToListAsync(cancellationToken);

        return rows.Where(r => r.Total > 0).ToDictionary(r => r.Key, r => Math.Round(r.Up * 100d / r.Total, 2));
    }

    public async Task<double?> GetAverageResponseMsAsync(Guid checkId, DateTime since, CancellationToken cancellationToken = default) =>
        await _context.UptimeCheckResults
            .Where(r => r.CheckId == checkId && r.CheckedAt >= since && r.IsUp)
            .Select(r => (double?)r.ResponseMs)
            .AverageAsync(cancellationToken);

    public Task<int> DeleteExpiredResultsAsync(DateTime cutoffUtc, int keepLatestPerCheck, CancellationToken cancellationToken = default) =>
        RetentionDeleter.DeleteExpiredAsync(_context, RetentionTarget.UptimeResults, cutoffUtc, keepLatestPerCheck, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
