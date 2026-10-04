namespace ServerManager.Application.DTOs.Security;

public sealed class SecurityOverviewDto
{
    public IReadOnlyList<SecurityServerSummaryDto> Servers { get; init; } = [];

    public int ScannedCount { get; init; }

    public int? AverageScore { get; init; }

    public int CriticalTotal { get; init; }

    public int WarningTotal { get; init; }

    public int ServersWithCritical { get; init; }

    public int ScanIntervalHours { get; init; }
}
