using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class ServerRepository : Repository<Server>, IServerRepository
{
    public ServerRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<Server?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbSet
            .Include(s => s.Credential)
            .Include(s => s.Tags)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<PagedResult<Server>> SearchAsync(ServerFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);

        var query = _dbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(s =>
                s.Name.Contains(search)
                || s.Hostname.Contains(search)
                || s.IpAddress.Contains(search)
                || (s.Provider != null && s.Provider.Contains(search))
                || (s.Location != null && s.Location.Contains(search)));
        }

        if (filter.Status.HasValue)
            query = query.Where(s => s.Status == filter.Status.Value);

        if (filter.Environment.HasValue)
            query = query.Where(s => s.Environment == filter.Environment.Value);

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tag = filter.Tag.Trim().ToLowerInvariant();
            query = query.Where(s => s.Tags.Any(t => t.Name == tag));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(s => s.Tags)
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PagedResult<Server>(items, totalCount, page, pageSize);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        _dbSet.AnyAsync(s => s.Name == name && (excludeId == null || s.Id != excludeId), cancellationToken);

    public async Task<IReadOnlyDictionary<ServerStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default) =>
        await _dbSet
            .AsNoTracking()
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<Server>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
        await _dbSet
            .AsNoTracking()
            .Include(s => s.Tags)
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetTagNamesAsync(CancellationToken cancellationToken = default) =>
        await _context.ServerTags
            .AsNoTracking()
            .Where(t => t.Server != null && !t.Server.IsDeleted)
            .Select(t => t.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetMonitorableIdsAsync(CancellationToken cancellationToken = default) =>
        await _dbSet
            .AsNoTracking()
            .Where(s => s.MonitoringEnabled && s.HostKeyFingerprint != null)
            .OrderBy(s => s.Name)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
}
