namespace ServerManager.Domain.Entities;

/// <summary>
/// Kaynak geçmişi: bir container'ın belirli andaki durumu ve kullanımı (docker stats + inspect). Ağ ve disk G/Ç değerleri
/// container açıldığından beri birikmiş sayaçlardır.
/// </summary>
public class ContainerMetricSample
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTime CollectedAt { get; set; }

    public string ContainerName { get; set; } = string.Empty;

    /// <summary>running, exited, restarting, paused…</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>healthy, unhealthy, starting; healthcheck yoksa null.</summary>
    public string? Health { get; set; }

    public int RestartCount { get; set; }

    public double CpuPercent { get; set; }

    public long MemoryUsageBytes { get; set; }

    public long MemoryLimitBytes { get; set; }

    public double MemoryPercent { get; set; }

    public long NetworkRxBytes { get; set; }

    public long NetworkTxBytes { get; set; }

    public long BlockReadBytes { get; set; }

    public long BlockWriteBytes { get; set; }
}
