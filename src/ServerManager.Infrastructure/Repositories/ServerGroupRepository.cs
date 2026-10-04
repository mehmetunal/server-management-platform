using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class ServerGroupRepository : IServerGroupRepository
{
    private readonly ApplicationDbContext _context;

    public ServerGroupRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServerGroup>> GetAllWithServersAsync(CancellationToken cancellationToken = default) =>
        await _context.ServerGroups
            .AsNoTracking()
            .Include(g => g.Servers)
            .OrderBy(g => g.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ServerGroup>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.ServerGroups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

    public Task<ServerGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ServerGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.ServerGroups.AnyAsync(g => g.Name == name && (excludeId == null || g.Id != excludeId), cancellationToken);

    public async Task<IReadOnlyList<Server>> GetServersAsync(IReadOnlyCollection<Guid> serverIds, CancellationToken cancellationToken = default) =>
        await _context.Servers
            .Where(s => serverIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Server>> GetGroupServersAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await _context.Servers
            .Where(s => s.GroupId == groupId)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ServerGroup group, CancellationToken cancellationToken = default) =>
        await _context.ServerGroups.AddAsync(group, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
