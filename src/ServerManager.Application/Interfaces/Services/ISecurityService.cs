using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Security;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Interfaces.Services;

public interface ISecurityService
{
    Task<SecurityOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<SecurityServerReportDto>> GetServerReportAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>Salt okunur tarama yapar ve sonucu kaydeder. Aynı sunucuda süren tarama varsa çakışma döner.</summary>
    Task<ServiceResult<SecurityScanSummaryDto>> ScanAsync(Guid serverId, SecurityScanTrigger trigger, CancellationToken cancellationToken = default);

    /// <summary>Host key'i doğrulanmış ve son taraması <paramref name="interval"/>'den eski (veya hiç taranmamış) sunucular.</summary>
    Task<IReadOnlyList<Guid>> GetDueServerIdsAsync(TimeSpan interval, CancellationToken cancellationToken = default);

    Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredAsync(int retentionDays, int keepLatestPerServer, CancellationToken cancellationToken = default);
}
