using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Monitoring;

public sealed class ServerMonitoringUpdateDto
{
    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public ServerStatus Status { get; init; }

    public ServerStatus PreviousStatus { get; init; }

    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public DateTime CheckedAt { get; init; }

    public ServerResourceSummaryDto? Latest { get; init; }

    public bool StatusChanged => Status != PreviousStatus;
}
