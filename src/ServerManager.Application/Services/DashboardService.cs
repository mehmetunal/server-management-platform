using ServerManager.Application.DTOs.Dashboard;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class DashboardService : IDashboardService
{
    private const int RecentItemCount = 8;

    private readonly IServerRepository _serverRepository;
    private readonly IAuditLogService _auditLogService;

    public DashboardService(IServerRepository serverRepository, IAuditLogService auditLogService)
    {
        _serverRepository = serverRepository;
        _auditLogService = auditLogService;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _serverRepository.GetStatusCountsAsync(cancellationToken);
        var recentServers = await _serverRepository.GetRecentAsync(RecentItemCount, cancellationToken);
        var recentEvents = await _auditLogService.GetRecentAsync(RecentItemCount, cancellationToken);

        int Count(ServerStatus status) => counts.TryGetValue(status, out var value) ? value : 0;

        return new DashboardSummaryDto
        {
            TotalServers = counts.Values.Sum(),
            HealthyServers = Count(ServerStatus.Healthy),
            WarningServers = Count(ServerStatus.Warning),
            CriticalServers = Count(ServerStatus.Critical),
            OfflineServers = Count(ServerStatus.Offline),
            MaintenanceServers = Count(ServerStatus.Maintenance),
            UnknownServers = Count(ServerStatus.Unknown),
            OnlineServers = Count(ServerStatus.Healthy) + Count(ServerStatus.Warning) + Count(ServerStatus.Critical),
            RecentServers = recentServers.Select(s => s.ToListItemDto()).ToList(),
            RecentEvents = recentEvents
        };
    }
}
