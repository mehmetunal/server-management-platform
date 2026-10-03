using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Application.DTOs.Dashboard;

public sealed class DashboardSummaryDto
{
    public int TotalServers { get; init; }

    public int OnlineServers { get; init; }

    public int OfflineServers { get; init; }

    public int CriticalServers { get; init; }

    public int WarningServers { get; init; }

    public int HealthyServers { get; init; }

    public int MaintenanceServers { get; init; }

    public int UnknownServers { get; init; }

    public FleetResourceSummaryDto Resources { get; init; } = new();

    public IReadOnlyList<ServerListItemDto> RecentServers { get; init; } = [];

    public IReadOnlyDictionary<Guid, ServerResourceSummaryDto> RecentServerResources { get; init; } = new Dictionary<Guid, ServerResourceSummaryDto>();

    public IReadOnlyList<AuditLogDto> RecentEvents { get; init; } = [];
}
