using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssl;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface ISslCertificateRepository
{
    Task<PagedResult<SslCertificateMonitor>> SearchAsync(SslMonitorFilterDto filter, CancellationToken cancellationToken = default);

    Task<SslCertificateMonitor?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string host, int port, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(SslCertificateMonitor monitor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTime checkedBefore, int limit, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
