using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class SecurityScanRepository : ISecurityScanRepository
{
    private readonly ApplicationDbContext _context;

    public SecurityScanRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SecurityScan scan, CancellationToken cancellationToken = default) =>
        await _context.SecurityScans.AddAsync(scan, cancellationToken);

    public Task<SecurityScan?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.SecurityScans.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<SecurityScan?> GetLatestCompletedAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        _context.SecurityScans
            .AsNoTracking()
            .Where(s => s.ServerId == serverId && s.Status == SecurityScanStatus.Completed)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SecurityScan>> GetHistoryAsync(Guid serverId, int take, CancellationToken cancellationToken = default) =>
        await _context.SecurityScans
            .AsNoTracking()
            .Where(s => s.ServerId == serverId)
            .OrderByDescending(s => s.StartedAt)
            .Take(take)
            .Select(WithoutReport())
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, SecurityScan>> GetLatestPerServerAsync(CancellationToken cancellationToken = default)
    {
        var scans = ActiveServerScans();
        var latest = await scans
            .Where(s => !scans.Any(other => other.ServerId == s.ServerId && other.StartedAt > s.StartedAt))
            .Select(WithoutReport())
            .ToListAsync(cancellationToken);
        return latest.GroupBy(s => s.ServerId).ToDictionary(g => g.Key, g => g.First());
    }

    public async Task<IReadOnlyList<SecurityScan>> GetLatestCompletedPerServerAsync(CancellationToken cancellationToken = default)
    {
        var completed = ActiveServerScans().Where(s => s.Status == SecurityScanStatus.Completed);
        var latest = await completed
            .Where(s => !completed.Any(other => other.ServerId == s.ServerId && other.StartedAt > s.StartedAt))
            .Select(WithoutReport())
            .ToListAsync(cancellationToken);
        return latest.GroupBy(s => s.ServerId).Select(g => g.First()).ToList();
    }

    public Task<bool> HasRunningAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        _context.SecurityScans.AnyAsync(s => s.ServerId == serverId && s.Status == SecurityScanStatus.Running, cancellationToken);

    public Task<int> InterruptRunningAsync(DateTime completedAt, string reason, CancellationToken cancellationToken = default) =>
        _context.SecurityScans
            .Where(s => s.Status == SecurityScanStatus.Running)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Status, SecurityScanStatus.Failed)
                .SetProperty(s => s.CompletedAt, completedAt)
                .SetProperty(s => s.FailureReason, reason), cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTime cutoffUtc, int keepLatestPerServer, CancellationToken cancellationToken = default) =>
        RetentionDeleter.DeleteExpiredAsync(_context, RetentionTarget.SecurityScans, cutoffUtc, keepLatestPerServer, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    private IQueryable<SecurityScan> ActiveServerScans() =>
        _context.SecurityScans
            .AsNoTracking()
            .Where(s => _context.Servers.Any(server => server.Id == s.ServerId));

    private static Expression<Func<SecurityScan, SecurityScan>> WithoutReport() => s => new SecurityScan
    {
        Id = s.Id,
        ServerId = s.ServerId,
        ServerName = s.ServerName,
        Trigger = s.Trigger,
        Status = s.Status,
        StartedAt = s.StartedAt,
        CompletedAt = s.CompletedAt,
        Score = s.Score,
        CriticalCount = s.CriticalCount,
        WarningCount = s.WarningCount,
        IsPrivileged = s.IsPrivileged,
        FailureReason = s.FailureReason,
        UserName = s.UserName
    };
}
