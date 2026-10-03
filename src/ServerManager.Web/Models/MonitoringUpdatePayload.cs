using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Web.Helpers;
using ServerManager.Web.Framework.UI;

namespace ServerManager.Web.Models;

public sealed class MonitoringUpdatePayload
{
    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public int Status { get; init; }

    public string StatusText { get; init; } = string.Empty;

    public string StatusBadgeClass { get; init; } = string.Empty;

    public bool StatusChanged { get; init; }

    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string CheckedAtText { get; init; } = string.Empty;

    public ResourcePayload? Latest { get; init; }

    public static MonitoringUpdatePayload From(ServerMonitoringUpdateDto update) => new()
    {
        ServerId = update.ServerId,
        ServerName = update.ServerName,
        Status = (int)update.Status,
        StatusText = ServerDisplay.StatusText(update.Status),
        StatusBadgeClass = ServerDisplay.StatusBadgeClass(update.Status),
        StatusChanged = update.StatusChanged,
        IsSuccess = update.IsSuccess,
        Message = update.Message,
        CheckedAtText = DateDisplay.Format(update.CheckedAt, "HH:mm:ss"),
        Latest = update.Latest is null ? null : ResourcePayload.From(update.Latest)
    };
}
