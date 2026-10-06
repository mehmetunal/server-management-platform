using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Monitoring;

public sealed class ServerResourceSummaryDto
{
    public Guid ServerId { get; init; }

    public DateTime CollectedAt { get; init; }

    public double CpuUsagePercent { get; init; }

    /// <summary>Son anlık görüntüdeki mantıksal CPU sayısı; bilinmiyorsa null.</summary>
    public int? CpuThreads { get; init; }

    public double MemoryUsagePercent { get; init; }

    public long MemoryUsedBytes { get; init; }

    public long MemoryTotalBytes { get; init; }

    /// <summary>En dolu bölümün doluluk yüzdesi.</summary>
    public double DiskUsagePercent { get; init; }

    /// <summary>En dolu bölümün kullanılan alanı.</summary>
    public long DiskUsedBytes { get; init; }

    /// <summary>En dolu bölümün toplam boyutu.</summary>
    public long DiskTotalBytes { get; init; }

    public double LoadAverage1 { get; init; }

    public double NetworkRxBytesPerSecond { get; init; }

    public double NetworkTxBytesPerSecond { get; init; }

    public long UptimeSeconds { get; init; }

    public ServerStatus? Status { get; init; }
}
