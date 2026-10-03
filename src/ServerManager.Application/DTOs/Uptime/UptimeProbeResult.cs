namespace ServerManager.Application.DTOs.Uptime;

public sealed record UptimeProbeResult(bool IsUp, int ResponseMs, int? StatusCode, string Message);
