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
            DiskTotalBytes = snapshot.Disks.Sum(d => d.TotalBytes),
            DiskUsedBytes = snapshot.Disks.Sum(d => d.UsedBytes),
            DiskUsagePercent = ServerStatusEvaluator.MaxDiskUsagePercent(snapshot),
            NetworkRxBytesPerSecond = physicalInterfaces.Sum(i => i.RxBytesPerSecond),
            NetworkTxBytesPerSecond = physicalInterfaces.Sum(i => i.TxBytesPerSecond),
            UptimeSeconds = snapshot.System.UptimeSeconds
        };
    }

    public static ServerResourceSummaryDto ToSummaryDto(this ServerMetric metric, ServerStatus? status) => new()
    {
        ServerId = metric.ServerId,
        CollectedAt = metric.CollectedAt,
        CpuUsagePercent = metric.CpuUsagePercent,
        MemoryUsagePercent = metric.MemoryUsagePercent,
        DiskUsagePercent = metric.DiskUsagePercent,
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
