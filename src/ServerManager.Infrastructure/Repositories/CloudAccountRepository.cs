using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class CloudAccountRepository : ICloudAccountRepository
{
    private readonly ApplicationDbContext _context;

    public CloudAccountRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CloudAccount>> GetAllWithServersAsync(CancellationToken cancellationToken = default) =>
        await _context.CloudAccounts
            .AsNoTracking()
            .Include(a => a.Servers)
            .OrderBy(a => a.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<CloudAccount?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.CloudAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<CloudAccount?> GetWithServersAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.CloudAccounts
            .Include(a => a.Servers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.CloudAccounts.AnyAsync(a => a.Name == name && (excludeId == null || a.Id != excludeId), cancellationToken);

    public async Task<IReadOnlyList<Server>> GetUnlinkedServersAsync(CancellationToken cancellationToken = default) =>
        await _context.Servers
            .Where(s => s.CloudAccountId == null)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CloudAccount account, CancellationToken cancellationToken = default) =>
        await _context.CloudAccounts.AddAsync(account, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
