using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class ProjectServiceLinkRepository : IProjectServiceLinkRepository
{
    private readonly ApplicationDbContext _context;

    public ProjectServiceLinkRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProjectServiceLink>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _context.ProjectServiceLinks
            .AsNoTracking()
            .Include(l => l.ManagedService)
            .Where(l => l.ProjectId == projectId)
            .OrderBy(l => l.ManagedService!.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectServiceLink>> ListByServiceAsync(Guid serviceId, CancellationToken cancellationToken = default) =>
        await _context.ProjectServiceLinks
            .AsNoTracking()
            .Include(l => l.Project)
            .Where(l => l.ManagedServiceId == serviceId)
            .OrderBy(l => l.Project!.Name)
            .ToListAsync(cancellationToken);

    public Task<ProjectServiceLink?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ProjectServiceLinks
            .Include(l => l.Project)
            .Include(l => l.ManagedService)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<ProjectServiceLink?> FindAnyAsync(Guid projectId, Guid serviceId, CancellationToken cancellationToken = default) =>
        _context.ProjectServiceLinks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.ProjectId == projectId && l.ManagedServiceId == serviceId, cancellationToken);

    public async Task AddAsync(ProjectServiceLink link, CancellationToken cancellationToken = default) =>
        await _context.ProjectServiceLinks.AddAsync(link, cancellationToken);

    public void Remove(ProjectServiceLink link) => _context.ProjectServiceLinks.Remove(link);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
