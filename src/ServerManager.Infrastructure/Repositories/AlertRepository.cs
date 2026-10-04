using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Alerting;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class AlertRepository : IAlertRepository
{
    private readonly ApplicationDbContext _context;

    public AlertRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AlertRule>> GetRulesAsync(CancellationToken cancellationToken = default) =>
        await _context.AlertRules
            .AsNoTracking()
            .Include(r => r.Server)
            .Include(r => r.Channels).ThenInclude(c => c.Channel)
            .OrderBy(r => r.Kind).ThenBy(r => r.Threshold).ThenBy(r => r.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AlertRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default) =>
        await _context.AlertRules
            .Include(r => r.Channels).ThenInclude(c => c.Channel)
            .Where(r => r.IsEnabled)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<AlertRule?> GetRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.AlertRules
            .Include(r => r.Server)
            .Include(r => r.Channels).ThenInclude(c => c.Channel)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> RuleNameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.AlertRules.AnyAsync(r => r.Name == name && (excludeId == null || r.Id != excludeId), cancellationToken);

    public async Task AddRuleAsync(AlertRule rule, CancellationToken cancellationToken = default) =>
        await _context.AlertRules.AddAsync(rule, cancellationToken);

    public void RemoveRuleChannels(IEnumerable<AlertRuleChannel> links) =>
        _context.AlertRuleChannels.RemoveRange(links);

    public async Task<IReadOnlyDictionary<Guid, int>> GetFiringCountsByRuleAsync(CancellationToken cancellationToken = default) =>
        await _context.AlertEvents
            .Where(e => e.Status == AlertEventStatus.Firing)
            .GroupBy(e => e.RuleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<NotificationChannel>> GetChannelsAsync(CancellationToken cancellationToken = default) =>
        await _context.NotificationChannels
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<NotificationChannel?> GetChannelAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.NotificationChannels.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ChannelNameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.NotificationChannels.AnyAsync(c => c.Name == name && (excludeId == null || c.Id != excludeId), cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetExistingChannelIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await _context.NotificationChannels
            .Where(c => ids.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddChannelAsync(NotificationChannel channel, CancellationToken cancellationToken = default) =>
        await _context.NotificationChannels.AddAsync(channel, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetRuleCountsByChannelAsync(CancellationToken cancellationToken = default) =>
        await _context.AlertRuleChannels
            .Where(link => _context.AlertRules.Any(r => r.Id == link.RuleId))
            .GroupBy(link => link.ChannelId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    public async Task<PagedResult<AlertEvent>> SearchEventsAsync(AlertEventFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);

        var query = _context.AlertEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(e =>
                e.RuleName.Contains(search)
                || e.TargetName.Contains(search)
                || (e.ServerName != null && e.ServerName.Contains(search))
                || e.Message.Contains(search));
        }

        if (filter.Status.HasValue)
            query = query.Where(e => e.Status == filter.Status.Value);

        if (filter.Severity.HasValue)
            query = query.Where(e => e.Severity == filter.Severity.Value);

        if (filter.ServerId.HasValue)
            query = query.Where(e => e.ServerId == filter.ServerId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.Status)
            .ThenByDescending(e => e.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AlertEvent>(items, totalCount, page, pageSize);
    }

    public Task<AlertEvent?> GetEventAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.AlertEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AlertEvent>> GetOpenEventsAsync(CancellationToken cancellationToken = default) =>
        await _context.AlertEvents
            .Where(e => e.Status == AlertEventStatus.Firing)
            .ToListAsync(cancellationToken);

    public async Task AddEventAsync(AlertEvent alert, CancellationToken cancellationToken = default) =>
        await _context.AlertEvents.AddAsync(alert, cancellationToken);

    public async Task<(int Firing, int Critical)> CountFiringAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _context.AlertEvents
            .Where(e => e.Status == AlertEventStatus.Firing)
            .GroupBy(e => e.Severity)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return (counts.Sum(c => c.Count), counts.Where(c => c.Key == AlertSeverity.Critical).Sum(c => c.Count));
    }

    public async Task<IReadOnlyList<AlertEvent>> GetLatestFiringAsync(int count, CancellationToken cancellationToken = default) =>
        await _context.AlertEvents
            .AsNoTracking()
            .Where(e => e.Status == AlertEventStatus.Firing)
            .OrderByDescending(e => e.Severity)
            .ThenByDescending(e => e.StartedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task AddDeliveryAsync(NotificationDelivery delivery, CancellationToken cancellationToken = default) =>
        await _context.NotificationDeliveries.AddAsync(delivery, cancellationToken);

    public async Task<IReadOnlyList<NotificationDelivery>> GetRecentDeliveriesAsync(int count, CancellationToken cancellationToken = default) =>
        await _context.NotificationDeliveries
            .AsNoTracking()
            .OrderByDescending(d => d.SentAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public Task<int> DeleteExpiredDeliveriesAsync(DateTime cutoffUtc, int keepLatestPerChannel, CancellationToken cancellationToken = default) =>
        RetentionDeleter.DeleteExpiredAsync(_context, RetentionTarget.NotificationDeliveries, cutoffUtc, keepLatestPerChannel, cancellationToken);

    public async Task<IReadOnlyList<AlertServerSnapshot>> GetServerSnapshotsAsync(CancellationToken cancellationToken = default) =>
        await _context.Servers
            .AsNoTracking()
            .Select(s => new AlertServerSnapshot(s.Id, s.Name, s.Status, s.LastSeenAt, s.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MetricSample>> GetMetricSamplesAsync(DateTime since, CancellationToken cancellationToken = default) =>
        await _context.ServerMetrics
            .AsNoTracking()
            .Where(m => m.CollectedAt >= since)
            .OrderBy(m => m.CollectedAt)
            .Select(m => new MetricSample(m.ServerId, m.CollectedAt, m.CpuUsagePercent, m.MemoryUsagePercent, m.DiskUsagePercent))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AlertUptimeSnapshot>> GetUptimeSnapshotsAsync(CancellationToken cancellationToken = default) =>
        await _context.UptimeChecks
            .AsNoTracking()
            .Where(u => u.IsEnabled)
            .Select(u => new AlertUptimeSnapshot(
                u.Id, u.Name, u.ServerId, u.Server != null ? u.Server.Name : null,
                u.Status, u.StatusChangedAt, u.ConsecutiveFailures, u.LastError))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AlertSslSnapshot>> GetSslSnapshotsAsync(CancellationToken cancellationToken = default) =>
        await _context.SslCertificateMonitors
            .AsNoTracking()
            .Where(m => m.IsEnabled)
            .Select(m => new AlertSslSnapshot(
                m.Id, m.Host, m.Port, m.ServerId, m.Server != null ? m.Server.Name : null, m.NotAfter))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AlertDeploymentSnapshot>> GetLatestFinishedDeploymentsAsync(CancellationToken cancellationToken = default)
    {
        var finished = _context.Deployments
            .AsNoTracking()
            .Where(d => d.Status == DeploymentStatus.Succeeded || d.Status == DeploymentStatus.Failed)
            .Where(d => _context.DeploymentProjects.Any(p => p.Id == d.ProjectId));

        return await finished
            .Where(d => !finished.Any(other => other.ProjectId == d.ProjectId && other.StartedAt > d.StartedAt))
            .Select(d => new AlertDeploymentSnapshot(
                d.ProjectId, d.ProjectName, d.ServerId, d.ServerName, d.Id, d.Status, d.FailureReason, d.CompletedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertBackupSnapshot>> GetLatestFinishedBackupsAsync(CancellationToken cancellationToken = default)
    {
        var finished = _context.BackupRuns
            .AsNoTracking()
            .Where(r => r.Operation == BackupOperation.Backup && r.JobId != null)
            .Where(r => r.Status == BackupRunStatus.Succeeded || r.Status == BackupRunStatus.Failed)
            .Where(r => _context.BackupJobs.Any(j => j.Id == r.JobId));

        return await finished
            .Where(r => !finished.Any(other => other.JobId == r.JobId && other.StartedAt > r.StartedAt))
            .Select(r => new AlertBackupSnapshot(
                r.JobId!.Value, r.JobName, r.ServerId, r.ServerName, r.Id, r.Status, r.FailureReason, r.CompletedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
