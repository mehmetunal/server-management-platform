using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<PagedResult<AuditLog>> SearchAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
