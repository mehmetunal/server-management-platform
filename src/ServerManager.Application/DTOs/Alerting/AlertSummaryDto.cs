namespace ServerManager.Application.DTOs.Alerting;

public sealed record AlertSummaryDto(int FiringCount, int CriticalCount, IReadOnlyList<AlertEventDto> Latest);
