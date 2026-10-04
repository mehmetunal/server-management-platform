using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class DeploymentDomainRepository : IDeploymentDomainRepository
{
    private readonly ApplicationDbContext _context;

    public DeploymentDomainRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DeploymentDomain>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _context.DeploymentDomains
            .Where(d => d.ProjectId == projectId)
            .OrderBy(d => d.Host)
            .ThenBy(d => d.Path)
            .ToListAsync(cancellationToken);

    public Task<DeploymentDomain?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.DeploymentDomains.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _context.DeploymentDomains.CountAsync(d => d.ProjectId == projectId, cancellationToken);

    public Task<bool> HostPathExistsAsync(Guid serverId, string host, string path, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.DeploymentDomains.AnyAsync(d =>
            d.ServerId == serverId
            && d.Host == host
            && d.Path == path
            && (excludeId == null || d.Id != excludeId), cancellationToken);

    public async Task AddAsync(DeploymentDomain domain, CancellationToken cancellationToken = default) =>
        await _context.DeploymentDomains.AddAsync(domain, cancellationToken);

    public async Task SoftDeleteByProjectAsync(Guid projectId, string? deletedBy, DateTime deletedAt, CancellationToken cancellationToken = default)
    {
        var domains = await _context.DeploymentDomains.Where(d => d.ProjectId == projectId).ToListAsync(cancellationToken);
        foreach (var domain in domains)
        {
            domain.IsDeleted = true;
            domain.DeletedAt = deletedAt;
            domain.DeletedBy = deletedBy;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
