using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class ManagedServiceRepository : IManagedServiceRepository
{
    private readonly ApplicationDbContext _context;

    public ManagedServiceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ManagedService>> ListAsync(Guid? serverId, CancellationToken cancellationToken = default)
    {
        var query = _context.ManagedServices.AsNoTracking().Include(s => s.Server).AsQueryable();
        if (serverId.HasValue)
            query = query.Where(s => s.ServerId == serverId.Value);

        return await query
            .Where(s => s.Server != null)
            .OrderBy(s => s.Server!.Name)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<ManagedService?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ManagedServices
            .Include(s => s.Server)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(Guid serverId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        _context.ManagedServices.AnyAsync(s => s.ServerId == serverId && s.Name == name && (excludeId == null || s.Id != excludeId), cancellationToken);

    public Task<bool> SlugExistsAsync(Guid serverId, string slug, CancellationToken cancellationToken = default) =>
        _context.ManagedServices.IgnoreQueryFilters().AnyAsync(s => s.ServerId == serverId && s.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<ManagedService>> ListOthersOnServerAsync(Guid serverId, Guid excludeId, CancellationToken cancellationToken = default) =>
        await _context.ManagedServices
            .AsNoTracking()
            .Where(s => s.ServerId == serverId && s.Id != excludeId && s.Status != ManagedServiceStatus.Removed)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ManagedService service, CancellationToken cancellationToken = default) =>
        await _context.ManagedServices.AddAsync(service, cancellationToken);

    public Task<ManagedServiceOperation?> GetOperationAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ManagedServiceOperations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<ManagedServiceOperation?> GetRunningOperationAsync(Guid serviceId, CancellationToken cancellationToken = default) =>
        _context.ManagedServiceOperations
            .AsNoTracking()
            .Where(o => o.ServiceId == serviceId && o.Status == ManagedServiceOperationStatus.Running)
            .OrderByDescending(o => o.StartedAt)
            .Select(WithoutLog())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ManagedServiceOperation>> ListOperationsAsync(Guid serviceId, int take, CancellationToken cancellationToken = default) =>
        await _context.ManagedServiceOperations
            .AsNoTracking()
            .Where(o => o.ServiceId == serviceId)
            .OrderByDescending(o => o.StartedAt)
            .Take(Math.Clamp(take, 1, 500))
            .Select(WithoutLog())
            .ToListAsync(cancellationToken);

    public async Task AddOperationAsync(ManagedServiceOperation operation, CancellationToken cancellationToken = default) =>
        await _context.ManagedServiceOperations.AddAsync(operation, cancellationToken);

    public Task UpdateOperationLogAsync(Guid id, string log, CancellationToken cancellationToken = default) =>
        _context.ManagedServiceOperations
            .Where(o => o.Id == id && o.Status == ManagedServiceOperationStatus.Running)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.Log, log), cancellationToken);

    public async Task<int> InterruptRunningAsync(DateTime finishedAt, string reason, CancellationToken cancellationToken = default)
    {
        var count = await _context.ManagedServiceOperations
            .Where(o => o.Status == ManagedServiceOperationStatus.Running)
            .ExecuteUpdateAsync(set => set
                .SetProperty(o => o.Status, ManagedServiceOperationStatus.Interrupted)
                .SetProperty(o => o.FinishedAt, finishedAt)
                .SetProperty(o => o.FailureReason, reason), cancellationToken);

        await _context.ManagedServices
            .Where(s => s.Status == ManagedServiceStatus.Installing || s.Status == ManagedServiceStatus.Updating || s.Status == ManagedServiceStatus.Removing)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Status, ManagedServiceStatus.Failed)
                .SetProperty(s => s.LastError, reason), cancellationToken);

        return count;
    }

    public async Task<bool> SetPendingAutoBackupAsync(Guid operationId, string? protectedOptions, CancellationToken cancellationToken = default) =>
        await _context.ManagedServiceOperations
            .Where(o => o.Id == operationId)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.PendingAutoBackup, protectedOptions), cancellationToken) > 0;

    public async Task<string?> ClaimPendingAutoBackupAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var value = await _context.ManagedServiceOperations
            .AsNoTracking()
            .Where(o => o.Id == operationId)
            .Select(o => o.PendingAutoBackup)
            .FirstOrDefaultAsync(cancellationToken);
        if (value is null)
            return null;

        var claimed = await _context.ManagedServiceOperations
            .Where(o => o.Id == operationId && o.PendingAutoBackup != null)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.PendingAutoBackup, (string?)null), cancellationToken);
        return claimed > 0 ? value : null;
    }

    public async Task<IReadOnlyList<ManagedServiceOperation>> ListPendingAutoBackupOperationsAsync(CancellationToken cancellationToken = default) =>
        await _context.ManagedServiceOperations
            .AsNoTracking()
            .Where(o => o.PendingAutoBackup != null && o.Status != ManagedServiceOperationStatus.Running)
            .OrderBy(o => o.StartedAt)
            .Select(WithoutLog())
            .ToListAsync(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    private static System.Linq.Expressions.Expression<Func<ManagedServiceOperation, ManagedServiceOperation>> WithoutLog() => o => new ManagedServiceOperation
    {
        Id = o.Id,
        ServiceId = o.ServiceId,
        ServerId = o.ServerId,
        ServiceName = o.ServiceName,
        Kind = o.Kind,
        Status = o.Status,
        Stage = o.Stage,
        FromTag = o.FromTag,
        ToTag = o.ToTag,
        RemoveData = o.RemoveData,
        FailureReason = o.FailureReason,
        UserId = o.UserId,
        UserName = o.UserName,
        IpAddress = o.IpAddress,
        StartedAt = o.StartedAt,
        FinishedAt = o.FinishedAt
    };
}
