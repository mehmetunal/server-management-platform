namespace ServerManager.Application.DTOs.Security;

public sealed class SecurityServerSummaryDto
{
    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public bool HostKeyVerified { get; init; }

    /// <summary>Sunucunun son taraması (başarısız olabilir); hiç taranmadıysa boş.</summary>
    public SecurityScanSummaryDto? LatestScan { get; init; }
}
