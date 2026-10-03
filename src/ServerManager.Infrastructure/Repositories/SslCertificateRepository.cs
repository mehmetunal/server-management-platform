using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssl;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class SslCertificateRepository : ISslCertificateRepository
{
    private readonly ApplicationDbContext _context;

    public SslCertificateRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SslCertificateMonitor>> SearchAsync(SslMonitorFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);

        var query = _context.SslCertificateMonitors.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(m =>
                m.Host.Contains(search)
                || (m.Subject != null && m.Subject.Contains(search))
                || (m.Issuer != null && m.Issuer.Contains(search))
                || (m.Server != null && m.Server.Name.Contains(search)));
        }

        if (filter.Status.HasValue)
            query = query.Where(m => m.Status == filter.Status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(m => m.Server)
            .OrderBy(m => m.NotAfter == null)
            .ThenBy(m => m.NotAfter)
            .ThenBy(m => m.Host)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SslCertificateMonitor>(items, totalCount, page, pageSize);
    }

    public Task<SslCertificateMonitor?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.SslCertificateMonitors
            .Include(m => m.Server)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(string host, int port, Guid? excludeId, CancellationToken cancellationToken = default) =>
        _context.SslCertificateMonitors.AnyAsync(m => m.Host == host && m.Port == port && (excludeId == null || m.Id != excludeId), cancellationToken);

    public async Task AddAsync(SslCertificateMonitor monitor, CancellationToken cancellationToken = default) =>
        await _context.SslCertificateMonitors.AddAsync(monitor, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTime checkedBefore, int limit, CancellationToken cancellationToken = default) =>
        await _context.SslCertificateMonitors
            .AsNoTracking()
            .Where(m => m.IsEnabled && (m.LastCheckedAt == null || m.LastCheckedAt < checkedBefore))
            .OrderBy(m => m.LastCheckedAt)
            .Take(limit)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
