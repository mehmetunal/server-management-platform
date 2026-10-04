using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;

namespace ServerManager.Application.Interfaces.Services;

public interface IAuditLogService
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    Task<AuditLogDto?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<AuditStatsDto> GetStatsAsync(TimeSpan window, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default);

    Task<AuditExportDto> ExportAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);

    Task<AuditChainVerificationDto> VerifyChainAsync(CancellationToken cancellationToken = default);
}
