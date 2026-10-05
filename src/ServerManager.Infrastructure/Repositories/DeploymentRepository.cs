using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class DeploymentRepository : IDeploymentRepository
{
    private static readonly DeploymentStatus[] RunningStatuses = [DeploymentStatus.Started, DeploymentStatus.Building, DeploymentStatus.Deploying];

    private readonly ApplicationDbContext _context;

    public DeploymentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<DeploymentProject>> SearchProjectsAsync(ProjectFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);

        var query = _context.DeploymentProjects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(p =>
                p.Name.Contains(search)
                || p.RepositoryUrl.Contains(search)
                || p.Branch.Contains(search)
                || p.Server!.Name.Contains(search));
        }

        if (filter.ServerId.HasValue)
            query = query.Where(p => p.ServerId == filter.ServerId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(p => p.Server)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DeploymentProject>(items, totalCount, page, pageSize);
    }

    public Task<DeploymentProject?> GetProjectAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.DeploymentProjects
            .Include(p => p.Server)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DeploymentProject>> GetProjectsByServerAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        await _context.DeploymentProjects
            .AsNoTracking()
            .Include(p => p.Server)
            .Where(p => p.ServerId == serverId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> ProjectExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.DeploymentProjects.AnyAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ProjectNameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        _context.DeploymentProjects.AnyAsync(p => p.Name == name && (excludeId == null || p.Id != excludeId), cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default) =>
        _context.DeploymentProjects.IgnoreQueryFilters().AnyAsync(p => p.Slug == slug, cancellationToken);

    public Task<bool> HasServiceLinksAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _context.ProjectServiceLinks.AnyAsync(l => l.ProjectId == projectId, cancellationToken);

    public async Task AddProjectAsync(DeploymentProject project, CancellationToken cancellationToken = default) =>
        await _context.DeploymentProjects.AddAsync(project, cancellationToken);

    public async Task<PagedResult<Deployment>> SearchDeploymentsAsync(DeploymentFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);

        var query = _context.Deployments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(d =>
                d.ProjectName.Contains(search)
                || d.ServerName.Contains(search)
                || d.Branch.Contains(search)
                || (d.CommitSha != null && d.CommitSha.StartsWith(search))
                || (d.CommitMessage != null && d.CommitMessage.Contains(search))
                || (d.UserName != null && d.UserName.Contains(search)));
        }

        if (filter.ProjectId.HasValue)
            query = query.Where(d => d.ProjectId == filter.ProjectId.Value);

        if (filter.ServerId.HasValue)
            query = query.Where(d => d.ServerId == filter.ServerId.Value);

        if (filter.Status.HasValue)
            query = query.Where(d => d.Status == filter.Status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(WithoutLog())
            .ToListAsync(cancellationToken);

        return new PagedResult<Deployment>(items, totalCount, page, pageSize);
    }

    public Task<Deployment?> GetDeploymentAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Deployments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Deployment?> GetRunningDeploymentAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _context.Deployments
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && RunningStatuses.Contains(d.Status))
            .OrderByDescending(d => d.StartedAt)
            .Select(WithoutLog())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Deployment>> GetLatestDeploymentsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
            return new Dictionary<Guid, Deployment>();

        var latestIds = await _context.Deployments
            .AsNoTracking()
            .Where(d => projectIds.Contains(d.ProjectId))
            .GroupBy(d => d.ProjectId)
            .Select(g => g.OrderByDescending(d => d.StartedAt).Select(d => d.Id).First())
            .ToListAsync(cancellationToken);

        var items = await _context.Deployments
            .AsNoTracking()
            .Where(d => latestIds.Contains(d.Id))
            .Select(WithoutLog())
            .ToListAsync(cancellationToken);

        return items.ToDictionary(d => d.ProjectId);
    }

    public Task<Deployment?> GetLastSuccessfulDeploymentAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _context.Deployments
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && d.Status == DeploymentStatus.Succeeded)
            .OrderByDescending(d => d.StartedAt)
            .Select(WithoutLog())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetCurrentDeploymentIdsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
            return new Dictionary<Guid, Guid>();

        var rows = await _context.Deployments
            .AsNoTracking()
            .Where(d => projectIds.Contains(d.ProjectId) && d.Status == DeploymentStatus.Succeeded)
            .GroupBy(d => d.ProjectId)
            .Select(g => new { ProjectId = g.Key, Id = g.OrderByDescending(d => d.StartedAt).Select(d => d.Id).First() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.ProjectId, r => r.Id);
    }

    public async Task AddDeploymentAsync(Deployment deployment, CancellationToken cancellationToken = default) =>
        await _context.Deployments.AddAsync(deployment, cancellationToken);

    public Task UpdateDeploymentLogAsync(Guid id, string log, CancellationToken cancellationToken = default) =>
        _context.Deployments
            .Where(d => d.Id == id && RunningStatuses.Contains(d.Status))
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.Log, log), cancellationToken);

    public Task<int> InterruptRunningDeploymentsAsync(DateTime completedAt, string reason, CancellationToken cancellationToken = default) =>
        _context.Deployments
            .Where(d => RunningStatuses.Contains(d.Status))
            .ExecuteUpdateAsync(set => set
                .SetProperty(d => d.Status, DeploymentStatus.Interrupted)
                .SetProperty(d => d.CompletedAt, completedAt)
                .SetProperty(d => d.FailureReason, reason), cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    private static System.Linq.Expressions.Expression<Func<Deployment, Deployment>> WithoutLog() => d => new Deployment
    {
        Id = d.Id,
        ProjectId = d.ProjectId,
        ProjectName = d.ProjectName,
        ServerId = d.ServerId,
        ServerName = d.ServerName,
        BuildType = d.BuildType,
        Branch = d.Branch,
        RequestedCommit = d.RequestedCommit,
        CommitSha = d.CommitSha,
        CommitMessage = d.CommitMessage,
        CommitAuthor = d.CommitAuthor,
        SourceDeploymentId = d.SourceDeploymentId,
        Kind = d.Kind,
        Status = d.Status,
        FailureReason = d.FailureReason,
        ExitCode = d.ExitCode,
        UserId = d.UserId,
        UserName = d.UserName,
        IpAddress = d.IpAddress,
        StartedAt = d.StartedAt,
        BuildStartedAt = d.BuildStartedAt,
        DeployStartedAt = d.DeployStartedAt,
        CompletedAt = d.CompletedAt,
        CancelledBy = d.CancelledBy
    };
}
