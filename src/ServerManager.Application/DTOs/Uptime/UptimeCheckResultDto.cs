namespace ServerManager.Application.DTOs.Uptime;

public sealed record UptimeCheckResultDto(DateTime CheckedAt, bool IsUp, int ResponseMs, int? StatusCode, string? Message);
