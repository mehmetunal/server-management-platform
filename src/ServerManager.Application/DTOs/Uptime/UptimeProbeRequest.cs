using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Uptime;

public sealed record UptimeProbeRequest(UptimeCheckType Type, string? Url, string? Host, int? Port, int TimeoutSeconds, string? AcceptedStatusCodes);
