using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Commands;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class CommandRunRepository : ICommandRunRepository
{
    private readonly ApplicationDbContext _context;

    public CommandRunRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<CommandRun>> SearchAsync(CommandRunFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);
        var query = _context.CommandRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(r =>
                r.Command.Contains(term)
                || (r.TemplateName != null && r.TemplateName.Contains(term))
                || (r.UserName != null && r.UserName.Contains(term))
                || r.Targets.Any(t => t.ServerName.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CommandRun>(items, total, page, pageSize);
    }

    public Task<CommandRun?> GetWithTargetsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.CommandRuns
            .Include(r => r.Targets)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CommandRun>> GetRunningAsync(CancellationToken cancellationToken = default) =>
        await _context.CommandRuns
            .Include(r => r.Targets)
            .Where(r => r.Status == CommandRunStatus.Running)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CommandRun run, CancellationToken cancellationToken = default) =>
        await _context.CommandRuns.AddAsync(run, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
