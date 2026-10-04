using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class ServerTemplateRepository : IServerTemplateRepository
{
    private readonly ApplicationDbContext _context;

    public ServerTemplateRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServerTemplate>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.ServerTemplates
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public Task<ServerTemplate?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ServerTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.ServerTemplates.AnyAsync(t => t.Name == name && (excludeId == null || t.Id != excludeId), cancellationToken);

    public async Task AddAsync(ServerTemplate template, CancellationToken cancellationToken = default) =>
        await _context.ServerTemplates.AddAsync(template, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
