using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Helpers;

namespace ServerManager.Web.Models;

public sealed class MetricSeriesModel
{
    public string Range { get; init; } = string.Empty;

    public IReadOnlyList<string> Labels { get; init; } = [];

    public IReadOnlyList<double> Cpu { get; init; } = [];

    public IReadOnlyList<double> Memory { get; init; } = [];

    public IReadOnlyList<double> Disk { get; init; } = [];

    public IReadOnlyList<double> Rx { get; init; } = [];

    public IReadOnlyList<double> Tx { get; init; } = [];

    public IReadOnlyList<double> Load { get; init; } = [];

    public static MetricSeriesModel From(MetricRange range, IReadOnlyList<MetricPointDto> points)
    {
        var labelFormat = MetricRanges.UsesHourlyData(range) || range == MetricRange.OneDay ? "dd.MM HH:mm" : "HH:mm";
        return new MetricSeriesModel
        {
            Range = MetricRanges.Code(range),
            Labels = points.Select(p => DateDisplay.Format(p.Timestamp, labelFormat)).ToList(),
            Cpu = points.Select(p => Math.Round(p.CpuUsagePercent, 1)).ToList(),
            Memory = points.Select(p => Math.Round(p.MemoryUsagePercent, 1)).ToList(),
            Disk = points.Select(p => Math.Round(p.DiskUsagePercent, 1)).ToList(),
            Rx = points.Select(p => Math.Round(p.NetworkRxBytesPerSecond)).ToList(),
            Tx = points.Select(p => Math.Round(p.NetworkTxBytesPerSecond)).ToList(),
            Load = points.Select(p => Math.Round(p.LoadAverage1, 2)).ToList()
        };
    }
}
