using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IAuditLogRepository auditLogRepository, ICurrentUserService currentUser, ILogger<AuditLogService> logger)
    {
        _auditLogRepository = auditLogRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            UserId = TextHelper.Truncate(entry.UserIdOverride ?? _currentUser.UserId, 64),
            UserName = TextHelper.Truncate(entry.UserNameOverride ?? _currentUser.UserName, 256),
            Action = TextHelper.Truncate(entry.Action, 128)!,
            EntityType = TextHelper.Truncate(entry.EntityType, 64),
            EntityId = TextHelper.Truncate(entry.EntityId, 64),
            TargetName = TextHelper.Truncate(entry.TargetName, 256),
            Details = TextHelper.Truncate(entry.Details, 2000),
            IpAddress = TextHelper.Truncate(entry.IpAddressOverride ?? _currentUser.IpAddress, 45),
            UserAgent = TextHelper.Truncate(_currentUser.UserAgent, 512),
            IsSuccess = entry.IsSuccess,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _auditLogRepository.AddAsync(log, cancellationToken);
            await _auditLogRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit log yazılamadı. Action: {Action}, EntityId: {EntityId}", entry.Action, entry.EntityId);
        }
    }

    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = await _auditLogRepository.SearchAsync(filter, cancellationToken);
        return page.Map(l => l.ToDto());
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogRepository.GetRecentAsync(count, cancellationToken);
        return logs.Select(l => l.ToDto()).ToList();
    }
}
