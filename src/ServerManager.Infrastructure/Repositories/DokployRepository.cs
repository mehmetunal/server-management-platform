using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class DokployRepository : Repository<DokployInstance>, IDokployRepository
{
    public DokployRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<DokployInstance?> GetByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(i => i.ServerId == serverId, cancellationToken);

    public async Task<IReadOnlyList<DokployInstallation>> GetInstallationsAsync(Guid serverId, int count, CancellationToken cancellationToken = default) =>
        await _context.DokployInstallations
            .AsNoTracking()
            .Where(i => i.ServerId == serverId)
            .OrderByDescending(i => i.StartedAt)
            .Take(count)
            .Select(i => new DokployInstallation
            {
                Id = i.Id,
                ServerId = i.ServerId,
                ServerName = i.ServerName,
                UserId = i.UserId,
                UserName = i.UserName,
                IpAddress = i.IpAddress,
                RequestedVersion = i.RequestedVersion,
                ScriptUrl = i.ScriptUrl,
                ScriptSha256 = i.ScriptSha256,
                Status = i.Status,
                ExitCode = i.ExitCode,
                FailureReason = i.FailureReason,
                StartedAt = i.StartedAt,
                CompletedAt = i.CompletedAt
            })
            .ToListAsync(cancellationToken);

    public Task<DokployInstallation?> GetInstallationAsync(Guid installationId, CancellationToken cancellationToken = default) =>
        _context.DokployInstallations.FirstOrDefaultAsync(i => i.Id == installationId, cancellationToken);

    public Task<DokployInstallation?> GetRunningInstallationAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        _context.DokployInstallations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ServerId == serverId && i.Status == DokployInstallationStatus.Running, cancellationToken);

    public async Task AddInstallationAsync(DokployInstallation installation, CancellationToken cancellationToken = default) =>
        await _context.DokployInstallations.AddAsync(installation, cancellationToken);

    public Task UpdateInstallationOutputAsync(Guid installationId, string output, CancellationToken cancellationToken = default) =>
        _context.DokployInstallations
            .Where(i => i.Id == installationId && i.Status == DokployInstallationStatus.Running)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.Output, output), cancellationToken);

    public Task<int> InterruptRunningInstallationsAsync(DateTime completedAt, string reason, CancellationToken cancellationToken = default) =>
        _context.DokployInstallations
            .Where(i => i.Status == DokployInstallationStatus.Running)
            .ExecuteUpdateAsync(set => set
                .SetProperty(i => i.Status, DokployInstallationStatus.Interrupted)
                .SetProperty(i => i.CompletedAt, completedAt)
                .SetProperty(i => i.FailureReason, reason), cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetServerIdsForHealthCheckAsync(CancellationToken cancellationToken = default) =>
        await _dbSet
            .AsNoTracking()
            .Join(
                _context.Servers.Where(s => s.HostKeyFingerprint != null),
                i => i.ServerId,
                s => s.Id,
                (i, _) => i.ServerId)
            .ToListAsync(cancellationToken);
}
