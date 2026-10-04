using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Mappings;

public static class AuditLogMappings
{
    public static AuditLogDto ToDto(this AuditLog log) => new()
    {
        Id = log.Id,
        UserId = log.UserId,
        UserName = log.UserName,
        Action = log.Action,
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        TargetName = log.TargetName,
        Details = log.Details,
        IpAddress = log.IpAddress,
        UserAgent = log.UserAgent,
        IsSuccess = log.IsSuccess,
        CreatedAt = log.CreatedAt,
        ChainHash = log.ChainHash
    };
}
