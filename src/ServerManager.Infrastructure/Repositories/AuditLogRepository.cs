using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Auditing;
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

        var query = ApplyFilter(_dbSet.AsNoTracking(), filter);

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

    public Task<AuditLog?> GetDetailsAsync(long id, CancellationToken cancellationToken = default) =>
        _dbSet.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> ExportAsync(
        AuditLogFilterDto filter, int maxRows, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(_dbSet.AsNoTracking(), filter);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(maxRows)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<AuditStatsDto> GetStatsAsync(
        DateTime sinceUtc, string failedLoginAction, int topActionCount, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(a => a.CreatedAt >= sinceUtc);

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Failed = g.Count(a => !a.IsSuccess),
                FailedLogins = g.Count(a => a.Action == failedLoginAction)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var activeUsers = await query
            .Where(a => a.UserId != null)
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var topActions = await query
            .GroupBy(a => a.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Action)
            .Take(topActionCount)
            .ToListAsync(cancellationToken);

        return new AuditStatsDto
        {
            SinceUtc = sinceUtc,
            TotalCount = totals?.Total ?? 0,
            FailedCount = totals?.Failed ?? 0,
            FailedLoginCount = totals?.FailedLogins ?? 0,
            ActiveUserCount = activeUsers,
            TopActions = topActions.Select(x => new AuditActionCount(x.Action, x.Count)).ToList()
        };
    }

    public async Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default) =>
        await _dbSet
            .AsNoTracking()
            .Where(a => a.EntityType != null)
            .Select(a => a.EntityType!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);

    public Task<string?> GetLastChainHashAsync(CancellationToken cancellationToken = default) =>
        _dbSet
            .AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Select(a => a.ChainHash)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditLog>> GetChainBatchAsync(long afterId, int take, CancellationToken cancellationToken = default) =>
        await _dbSet
            .AsNoTracking()
            .Where(a => a.Id > afterId)
            .OrderBy(a => a.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task RunInChainLockAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        // Çağıranın açık transaction'ı varsa ona katılınır; kilit o transaction bitene kadar tutulur.
        if (_context.Database.CurrentTransaction is not null)
        {
            await AcquireChainLockAsync(cancellationToken);
            await action(cancellationToken);
            return;
        }

        // Yeniden deneme stratejisi (EnableRetryOnFailure) kullanıcı transaction'ını tek parça olarak tekrarlamayı gerektirir.
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            (Context: _context, Action: action),
            async (_, state, ct) =>
            {
                await using var transaction = await state.Context.Database.BeginTransactionAsync(ct);
                await AcquireChainLockAsync(ct);
                await state.Action(ct);
                await transaction.CommitAsync(ct);
                return true;
            },
            verifySucceeded: null,
            cancellationToken);
    }

    public Task<AuditChainAnchor?> GetChainAnchorAsync(CancellationToken cancellationToken = default) =>
        _context.AuditChainAnchors
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == AuditChainAnchor.SingletonId, cancellationToken);

    public async Task SaveChainAnchorAsync(AuditChainAnchor anchor, CancellationToken cancellationToken = default)
    {
        var updated = await _context.AuditChainAnchors
            .Where(a => a.Id == anchor.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.FirstSignedId, anchor.FirstSignedId)
                .SetProperty(a => a.SigningStartedAt, anchor.SigningStartedAt)
                .SetProperty(a => a.LastId, anchor.LastId)
                .SetProperty(a => a.LastHash, anchor.LastHash)
                .SetProperty(a => a.SignedCount, anchor.SignedCount)
                .SetProperty(a => a.UpdatedAt, anchor.UpdatedAt)
                .SetProperty(a => a.Signature, anchor.Signature), cancellationToken);
        if (updated > 0)
            return;

        var entry = _context.AuditChainAnchors.Add(anchor);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task<AuditChainSummary?> GetChainSummaryAsync(CancellationToken cancellationToken = default)
    {
        var signed = _dbSet.AsNoTracking().Where(a => a.ChainHash != null);
        var first = await signed.OrderBy(a => a.Id).Select(a => new { a.Id, a.CreatedAt }).FirstOrDefaultAsync(cancellationToken);
        if (first is null)
            return null;

        var last = await signed.OrderByDescending(a => a.Id).Select(a => new { a.Id, a.ChainHash }).FirstAsync(cancellationToken);
        var count = await signed.LongCountAsync(cancellationToken);
        return new AuditChainSummary(first.Id, first.CreatedAt, last.Id, last.ChainHash!, count);
    }

    public void Detach(AuditLog log)
    {
        var entry = _context.Entry(log);
        if (entry.State != EntityState.Detached)
            entry.State = EntityState.Detached;
    }

    private async Task AcquireChainLockAsync(CancellationToken cancellationToken)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await _context.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = N'ServerManager.AuditChain', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;",
            [result],
            cancellationToken);

        // 0: hemen alındı, 1: bekleyip alındı; negatif değerler zaman aşımı/iptal/kilitlenme.
        if (result.Value is not int code || code < 0)
            throw new InvalidOperationException($"Audit zincir kilidi alınamadı (sp_getapplock sonucu: {result.Value}).");
    }

    private static IQueryable<AuditLog> ApplyFilter(IQueryable<AuditLog> query, AuditLogFilterDto filter)
    {
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

        if (!string.IsNullOrWhiteSpace(filter.User))
        {
            var user = filter.User.Trim();
            query = query.Where(a => a.UserName != null && a.UserName.Contains(user));
        }

        if (!string.IsNullOrWhiteSpace(filter.Ip))
        {
            var ip = filter.Ip.Trim();
            query = query.Where(a => a.IpAddress != null && a.IpAddress.StartsWith(ip));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
            query = query.Where(a => a.EntityId == filter.EntityId);

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

        return query;
    }
}
