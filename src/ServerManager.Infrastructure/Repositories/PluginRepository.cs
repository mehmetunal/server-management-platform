using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class PluginRepository : IPluginRepository
{
    private readonly ApplicationDbContext _context;

    public PluginRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<InstalledPlugin>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.InstalledPlugins.ToListAsync(cancellationToken);

    public Task<InstalledPlugin?> GetAsync(string systemName, CancellationToken cancellationToken = default) =>
        _context.InstalledPlugins.FirstOrDefaultAsync(p => p.SystemName == systemName, cancellationToken);

    public async Task AddAsync(InstalledPlugin plugin, CancellationToken cancellationToken = default) =>
        await _context.InstalledPlugins.AddAsync(plugin, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
