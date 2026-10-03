using ServerManager.Application.DTOs.AuditLogs;
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

    public IReadOnlyList<ServerListItemDto> RecentServers { get; init; } = [];

    public IReadOnlyList<AuditLogDto> RecentEvents { get; init; } = [];
}
