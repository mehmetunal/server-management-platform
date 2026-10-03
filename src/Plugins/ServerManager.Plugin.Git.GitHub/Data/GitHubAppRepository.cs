using Microsoft.EntityFrameworkCore;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Infrastructure.Repositories;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Domain;
using ServerManager.Plugin.Git.GitHub.Services;

namespace ServerManager.Plugin.Git.GitHub.Data;

public class GitHubAppRepository : Repository<GitHubApp>, IGitHubAppRepository
{
    public GitHubAppRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<GitHubApp>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbSet.AsNoTracking().OrderBy(a => a.Name).ThenBy(a => a.CreatedAt).ToListAsync(cancellationToken);

    public Task<GitHubApp?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> ExistsByAppIdAsync(long appId, CancellationToken cancellationToken = default) =>
        _dbSet.AnyAsync(a => a.AppId == appId, cancellationToken);

    public async Task<IReadOnlyList<string>> GetProjectNamesAsync(Guid appRecordId, CancellationToken cancellationToken = default)
    {
        var prefix = GitHubSourceIds.Prefix(appRecordId);
        return await _context.DeploymentProjects
            .AsNoTracking()
            .Where(p => p.GitIntegration == GitHubPlugin.SystemName && p.GitSourceId != null && p.GitSourceId.StartsWith(prefix))
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}
