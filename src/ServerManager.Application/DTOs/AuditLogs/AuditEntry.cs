namespace ServerManager.Application.DTOs.AuditLogs;

public sealed record AuditEntry(
    string Action,
    string? EntityType = null,
    string? EntityId = null,
    string? TargetName = null,
    string? Details = null,
    bool IsSuccess = true,
    string? UserNameOverride = null);
