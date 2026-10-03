using ServerManager.Application.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Alerting;

public sealed class AlertEventFilterDto
{
    public string? Search { get; set; }

    /// <summary>Boşsa tüm alarmlar; varsayılan liste açık alarmlarla açılır.</summary>
    public AlertEventStatus? Status { get; set; }

    public AlertSeverity? Severity { get; set; }

    public Guid? ServerId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}
