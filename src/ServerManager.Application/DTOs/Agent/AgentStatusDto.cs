namespace ServerManager.Application.DTOs.Agent;

public sealed record AgentStatusDto(
    Guid ServerId,
    string ServerName,
    bool HasToken,
    DateTime? TokenCreatedAt,
    DateTime? LastSeenAt,
    string? Version,
    bool IsActive,
    bool MonitoringEnabled);
