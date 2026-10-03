using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Alerting;

public sealed class AlertRuleFormDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public AlertRuleKind Kind { get; set; } = AlertRuleKind.CpuUsage;

    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;

    public double Threshold { get; set; } = 90;

    public int DurationMinutes { get; set; } = 5;

    public Guid? ServerId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool NotifyRecovery { get; set; } = true;

    public int RepeatIntervalMinutes { get; set; }

    public List<Guid> ChannelIds { get; set; } = [];
}
