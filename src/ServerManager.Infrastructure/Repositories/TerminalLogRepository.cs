using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class TerminalLogRepository : Repository<TerminalSessionLog>, ITerminalLogRepository
{
    public TerminalLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<TerminalSessionLog>> GetSessionsAsync(Guid serverId, string? userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Paging.NormalizePage(page);
        pageSize = Paging.NormalizePageSize(pageSize);

        var query = _dbSet.AsNoTracking().Where(s => s.ServerId == serverId);
        if (userId is not null)
            query = query.Where(s => s.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TerminalSessionLog>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<TerminalCommandLog>> GetCommandsAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        await _context.TerminalCommands
            .AsNoTracking()
            .Where(c => c.SessionId == sessionId)
            .OrderBy(c => c.ExecutedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetRecentCommandTextsAsync(Guid serverId, string userId, int count, CancellationToken cancellationToken = default) =>
        await _context.TerminalCommands
            .AsNoTracking()
            .Where(c => !c.IsApproximate && c.CommandText != string.Empty)
            .Join(
                _dbSet.Where(s => s.ServerId == serverId && s.UserId == userId),
                c => c.SessionId,
                s => s.Id,
                (c, _) => c)
            .GroupBy(c => c.CommandText)
            .Select(g => new { Text = g.Key, LastAt = g.Max(c => c.ExecutedAt) })
            .OrderByDescending(x => x.LastAt)
            .Take(count)
            .Select(x => x.Text)
            .ToListAsync(cancellationToken);

    public async Task AddCommandsAsync(IEnumerable<TerminalCommandLog> commands, CancellationToken cancellationToken = default) =>
        await _context.TerminalCommands.AddRangeAsync(commands, cancellationToken);

    public async Task IncrementCommandCountsAsync(IReadOnlyDictionary<Guid, int> countsBySession, CancellationToken cancellationToken = default)
    {
        foreach (var (sessionId, count) in countsBySession)
        {
            await _dbSet
                .Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.CommandCount, s => s.CommandCount + count), cancellationToken);
        }
    }

    public Task CloseSessionAsync(Guid sessionId, DateTime endedAt, string? closeReason, CancellationToken cancellationToken = default) =>
        _dbSet
            .Where(s => s.Id == sessionId && s.EndedAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.EndedAt, endedAt)
                .SetProperty(s => s.CloseReason, closeReason), cancellationToken);

    public Task<int> CloseOpenSessionsAsync(DateTime startedBefore, DateTime endedAt, string closeReason, CancellationToken cancellationToken = default) =>
        _dbSet
            .Where(s => s.EndedAt == null && s.StartedAt < startedBefore)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.EndedAt, endedAt)
                .SetProperty(s => s.CloseReason, closeReason), cancellationToken);
}
