using ServerManager.Application.Security;

namespace ServerManager.Application.DTOs.Security;

public sealed class SecurityServerReportDto
{
    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public bool HostKeyVerified { get; init; }

    public bool UseSudo { get; init; }

    /// <summary>Son tamamlanan tarama ve raporu; hiç tamamlanmadıysa boş.</summary>
    public SecurityScanSummaryDto? CompletedScan { get; init; }

    public SecurityReport? Report { get; init; }

    /// <summary>Son deneme tamamlanan taramadan yeniyse ve başarısızsa gösterilir.</summary>
    public SecurityScanSummaryDto? LatestFailure { get; init; }

    public bool IsRunning { get; init; }

    public IReadOnlyList<SecurityScanSummaryDto> History { get; init; } = [];
}
