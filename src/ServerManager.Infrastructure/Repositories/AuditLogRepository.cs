using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<AuditLog>> SearchAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);

        var query = _dbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(a =>
                (a.UserName != null && a.UserName.Contains(search))
                || (a.TargetName != null && a.TargetName.Contains(search))
                || (a.IpAddress != null && a.IpAddress.Contains(search))
                || (a.Details != null && a.Details.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(a => a.Action == filter.Action);

        if (filter.IsSuccess.HasValue)
            query = query.Where(a => a.IsSuccess == filter.IsSuccess.Value);

        if (filter.From.HasValue)
        {
            var from = AppTimeZone.ToUtc(filter.From.Value.Date);
            query = query.Where(a => a.CreatedAt >= from);
        }

        if (filter.To.HasValue)
        {
            var toExclusive = AppTimeZone.ToUtc(filter.To.Value.Date.AddDays(1));
            query = query.Where(a => a.CreatedAt < toExclusive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLog>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
        await _dbSet
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
}
