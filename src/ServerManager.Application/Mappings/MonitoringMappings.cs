using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Mappings;

public static class MonitoringMappings
{
    public static ServerMetric ToMetric(this SystemMetricsSnapshot snapshot, Guid serverId, DateTime collectedAt)
    {
        var physicalInterfaces = snapshot.NetworkInterfaces.Where(i => !i.IsVirtual).ToList();
        // Disk yüzdesi en dolu bölümden geldiği için boyutlar da aynı bölümden alınır; "kullanılan / toplam" ile yüzde tutarlı olur.
        var fullestDisk = snapshot.Disks.MaxBy(d => d.UsagePercent);

        return new ServerMetric
        {
            ServerId = serverId,
            CollectedAt = collectedAt,
            CpuUsagePercent = snapshot.CpuUsagePercent,
            LoadAverage1 = snapshot.LoadAverage1,
            LoadAverage5 = snapshot.LoadAverage5,
            LoadAverage15 = snapshot.LoadAverage15,
            MemoryTotalBytes = snapshot.MemoryTotalBytes,
            MemoryUsedBytes = snapshot.MemoryUsedBytes,
            MemoryUsagePercent = snapshot.MemoryUsagePercent,
            SwapTotalBytes = snapshot.SwapTotalBytes,
            SwapUsedBytes = snapshot.SwapUsedBytes,
            DiskTotalBytes = fullestDisk?.TotalBytes ?? 0,
            DiskUsedBytes = fullestDisk?.UsedBytes ?? 0,
            DiskUsagePercent = ServerStatusEvaluator.MaxDiskUsagePercent(snapshot),
            NetworkRxBytesPerSecond = physicalInterfaces.Sum(i => i.RxBytesPerSecond),
            NetworkTxBytesPerSecond = physicalInterfaces.Sum(i => i.TxBytesPerSecond),
            UptimeSeconds = snapshot.System.UptimeSeconds
        };
    }

    public static ServerResourceSummaryDto ToSummaryDto(this ServerMetric metric, ServerStatus? status, int? cpuThreads = null) => new()
    {
        ServerId = metric.ServerId,
        CollectedAt = metric.CollectedAt,
        CpuUsagePercent = metric.CpuUsagePercent,
        CpuThreads = cpuThreads is > 0 ? cpuThreads : null,
        MemoryUsagePercent = metric.MemoryUsagePercent,
        MemoryUsedBytes = metric.MemoryUsedBytes,
        MemoryTotalBytes = metric.MemoryTotalBytes,
        DiskUsagePercent = metric.DiskUsagePercent,
        DiskUsedBytes = metric.DiskUsedBytes,
        DiskTotalBytes = metric.DiskTotalBytes,
        LoadAverage1 = metric.LoadAverage1,
        NetworkRxBytesPerSecond = metric.NetworkRxBytesPerSecond,
        NetworkTxBytesPerSecond = metric.NetworkTxBytesPerSecond,
        UptimeSeconds = metric.UptimeSeconds,
        Status = status
    };

    public static HealthCheckDto ToDto(this ServerHealthCheck healthCheck) => new()
    {
        CheckedAt = healthCheck.CheckedAt,
        IsSuccess = healthCheck.IsSuccess,
        ResponseTimeMs = healthCheck.ResponseTimeMs,
        Message = healthCheck.Message
    };
}
