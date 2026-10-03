using System.Globalization;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Monitoring;

public static class ServerStatusEvaluator
{
    public static double MaxDiskUsagePercent(SystemMetricsSnapshot snapshot) =>
        snapshot.Disks.Count == 0 ? 0 : snapshot.Disks.Max(d => d.UsagePercent);

    public static StatusEvaluation Evaluate(SystemMetricsSnapshot snapshot, MonitoringOptions options)
    {
        var critical = new List<string>();
        var warning = new List<string>();

        Check("CPU", snapshot.CpuUsagePercent, options.CpuWarningPercent, options.CpuCriticalPercent);
        Check("RAM", snapshot.MemoryUsagePercent, options.MemoryWarningPercent, options.MemoryCriticalPercent);

        foreach (var disk in snapshot.Disks)
            Check($"Disk {disk.MountPoint}", disk.UsagePercent, options.DiskWarningPercent, options.DiskCriticalPercent);

        if (critical.Count > 0)
            return new StatusEvaluation(ServerStatus.Critical, critical.Concat(warning).ToList());

        return warning.Count > 0
            ? new StatusEvaluation(ServerStatus.Warning, warning)
            : new StatusEvaluation(ServerStatus.Healthy, []);

        void Check(string label, double value, double warningThreshold, double criticalThreshold)
        {
            var text = $"{label} %{value.ToString("0.#", CultureInfo.InvariantCulture)}";
            if (value >= criticalThreshold)
                critical.Add(text);
            else if (value >= warningThreshold)
                warning.Add(text);
        }
    }
}
