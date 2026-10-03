using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Mappings;

public static class AuditLogMappings
{
    public static AuditLogDto ToDto(this AuditLog log) => new()
    {
        Id = log.Id,
        UserName = log.UserName,
        Action = log.Action,
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        TargetName = log.TargetName,
        Details = log.Details,
        IpAddress = log.IpAddress,
        IsSuccess = log.IsSuccess,
        CreatedAt = log.CreatedAt
    };
}
