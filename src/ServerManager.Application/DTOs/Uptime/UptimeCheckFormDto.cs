using ServerManager.Application.Alerting;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Uptime;

public sealed class UptimeCheckFormDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public UptimeCheckType Type { get; set; } = UptimeCheckType.Http;

    public string? Url { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    public string? AcceptedStatusCodes { get; set; } = StatusCodeRanges.Default;

    public Guid? ServerId { get; set; }

    public int IntervalSeconds { get; set; } = 60;

    public int TimeoutSeconds { get; set; } = 10;

    public bool IsEnabled { get; set; } = true;
}
