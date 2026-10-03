namespace ServerManager.Application.DTOs.Uptime;

public sealed record UptimeCheckDetailsDto(
    UptimeCheckListItemDto Check,
    int TimeoutSeconds,
    string? AcceptedStatusCodes,
    double? UptimePercent7d,
    double? UptimePercent30d,
    double? AverageResponseMs24h,
    IReadOnlyList<UptimeCheckResultDto> RecentResults);
