using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class BackupRepository : IBackupRepository
{
    private readonly ApplicationDbContext _context;

    public BackupRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<BackupStorage>> GetStoragesAsync(CancellationToken cancellationToken = default) =>
        await _context.BackupStorages
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public Task<BackupStorage?> GetStorageAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.BackupStorages.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<BackupStorage?> GetStorageIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.BackupStorages.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> StorageNameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.BackupStorages.AnyAsync(s => s.Name == name && (excludeId == null || s.Id != excludeId), cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetJobCountsByStorageAsync(CancellationToken cancellationToken = default) =>
        await _context.BackupJobs
            .AsNoTracking()
            .GroupBy(j => j.StorageId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    public async Task AddStorageAsync(BackupStorage storage, CancellationToken cancellationToken = default) =>
        await _context.BackupStorages.AddAsync(storage, cancellationToken);

    public async Task<IReadOnlyList<BackupJob>> GetJobsAsync(Guid? serverId, CancellationToken cancellationToken = default) =>
        await _context.BackupJobs
            .AsNoTracking()
            .Include(j => j.Server)
            .Include(j => j.Storage)
            .Where(j => serverId == null || j.ServerId == serverId)
            .OrderBy(j => j.Name)
            .ToListAsync(cancellationToken);

    public Task<BackupJob?> GetJobAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.BackupJobs
            .Include(j => j.Server)
            .Include(j => j.Storage)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<BackupJob?> GetJobIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.BackupJobs
            .IgnoreQueryFilters()
            .Include(j => j.Server)
            .Include(j => j.Storage)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<bool> JobNameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.BackupJobs.IgnoreQueryFilters()
            .AnyAsync(j => !j.IsDeleted && j.Name == name && (excludeId == null || j.Id != excludeId), cancellationToken);

    public async Task AddJobAsync(BackupJob job, CancellationToken cancellationToken = default) =>
        await _context.BackupJobs.AddAsync(job, cancellationToken);

    public async Task<IReadOnlyList<BackupJob>> GetDueJobsAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) =>
        await _context.BackupJobs
            .Where(j => j.IsEnabled && j.ScheduleType != BackupScheduleType.Manual && j.NextRunAt != null && j.NextRunAt <= nowUtc)
            .OrderBy(j => j.NextRunAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task AddRunAsync(BackupRun run, CancellationToken cancellationToken = default) =>
        await _context.BackupRuns.AddAsync(run, cancellationToken);

    public Task<BackupRun?> GetRunAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.BackupRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<BackupRun> Items, int TotalCount)> SearchRunsAsync(
        BackupRunFilterDto filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BackupRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(r =>
                r.JobName.Contains(search)
                || r.ServerName.Contains(search)
                || r.StorageName.Contains(search)
                || (r.FileName != null && r.FileName.Contains(search))
                || (r.UserName != null && r.UserName.Contains(search)));
        }

        if (filter.JobId.HasValue)
            query = query.Where(r => r.JobId == filter.JobId.Value);
        if (filter.ServerId.HasValue)
            query = query.Where(r => r.ServerId == filter.ServerId.Value);
        if (filter.Operation.HasValue)
            query = query.Where(r => r.Operation == filter.Operation.Value);
        if (filter.Status.HasValue)
            query = query.Where(r => r.Status == filter.Status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(WithoutLog())
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<bool> HasRunningBackupAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _context.BackupRuns.AnyAsync(
            r => r.JobId == jobId && r.Operation == BackupOperation.Backup && r.Status == BackupRunStatus.Running,
            cancellationToken);

    public async Task<IReadOnlyList<BackupRun>> GetRecentRunsAsync(Guid jobId, int count, CancellationToken cancellationToken = default) =>
        await _context.BackupRuns
            .AsNoTracking()
            .Where(r => r.JobId == jobId)
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .Select(WithoutLog())
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BackupRun>> GetAvailableBackupsAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await _context.BackupRuns
            .Where(r => r.JobId == jobId
                        && r.Operation == BackupOperation.Backup
                        && r.Status == BackupRunStatus.Succeeded
                        && r.ObjectKey != null
                        && r.ArtifactDeletedAt == null)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, BackupArtifactRef>> GetLatestAvailableBackupsAsync(IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default)
    {
        var ids = jobIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, BackupArtifactRef>();

        var latest = await _context.BackupRuns
            .AsNoTracking()
            .Where(r => r.JobId.HasValue
                        && ids.Contains(r.JobId.Value)
                        && r.Operation == BackupOperation.Backup
                        && r.Status == BackupRunStatus.Succeeded
                        && r.ObjectKey != null
                        && r.ArtifactDeletedAt == null)
            .GroupBy(r => r.JobId!.Value)
            .Select(g => g
                .OrderByDescending(r => r.StartedAt)
                .Select(r => new { JobId = r.JobId!.Value, r.Id, r.StartedAt, r.IsEncrypted, r.SizeBytes })
                .First())
            .ToListAsync(cancellationToken);

        return latest.ToDictionary(r => r.JobId, r => new BackupArtifactRef(r.JobId, r.Id, r.StartedAt, r.IsEncrypted, r.SizeBytes));
    }

    public Task UpdateRunLogAsync(Guid id, string log, CancellationToken cancellationToken = default) =>
        _context.BackupRuns
            .Where(r => r.Id == id && r.Status == BackupRunStatus.Running)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.Log, log), cancellationToken);

    public Task<int> InterruptRunningAsync(DateTime completedAt, string reason, CancellationToken cancellationToken = default) =>
        _context.BackupRuns
            .Where(r => r.Status == BackupRunStatus.Running)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.Status, BackupRunStatus.Interrupted)
                .SetProperty(r => r.CompletedAt, completedAt)
                .SetProperty(r => r.FailureReason, reason), cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    private static Expression<Func<BackupRun, BackupRun>> WithoutLog() => r => new BackupRun
    {
        Id = r.Id,
        Operation = r.Operation,
        Trigger = r.Trigger,
        Status = r.Status,
        JobId = r.JobId,
        JobName = r.JobName,
        SourceType = r.SourceType,
        ServerId = r.ServerId,
        ServerName = r.ServerName,
        StorageId = r.StorageId,
        StorageName = r.StorageName,
        ObjectKey = r.ObjectKey,
        FileName = r.FileName,
        SizeBytes = r.SizeBytes,
        Sha256 = r.Sha256,
        IsEncrypted = r.IsEncrypted,
        SourceRunId = r.SourceRunId,
        RestoreTarget = r.RestoreTarget,
        FailureReason = r.FailureReason,
        UserName = r.UserName,
        StartedAt = r.StartedAt,
        CompletedAt = r.CompletedAt,
        CancelledBy = r.CancelledBy,
        ArtifactDeletedAt = r.ArtifactDeletedAt,
        ArtifactDeletedBy = r.ArtifactDeletedBy
    };
}
