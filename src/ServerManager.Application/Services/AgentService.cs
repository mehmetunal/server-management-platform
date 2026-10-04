using Microsoft.Extensions.Logging;
using ServerManager.Application.Agent;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Agent;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class AgentService : IAgentService
{
    private const string NotFoundMessage = "Sunucu bulunamadı.";

    private readonly IServerRepository _serverRepository;
    private readonly IMonitoringService _monitoringService;
    private readonly IAgentReportParser _parser;
    private readonly IAuditLogService _auditLogService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AgentService> _logger;

    public AgentService(
        IServerRepository serverRepository,
        IMonitoringService monitoringService,
        IAgentReportParser parser,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        ILogger<AgentService> logger)
    {
        _serverRepository = serverRepository;
        _monitoringService = monitoringService;
        _parser = parser;
        _auditLogService = auditLogService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<ServiceResult<AgentStatusDto>> GetStatusAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        return server is null
            ? ServiceResult<AgentStatusDto>.NotFound(NotFoundMessage)
            : ServiceResult<AgentStatusDto>.Success(ToStatus(server));
    }

    public async Task<ServiceResult<AgentTokenDto>> CreateTokenAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult<AgentTokenDto>.NotFound(NotFoundMessage);

        var replaced = server.AgentTokenHash is not null;
        var token = AgentTokens.Generate();
        server.AgentTokenHash = AgentTokens.Hash(token);
        server.AgentTokenCreatedAt = UtcNow;
        server.AgentLastSeenAt = null;
        server.AgentVersion = null;
        await _serverRepository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.AgentTokenCreate, server, replaced ? "Önceki token geçersiz kılındı." : null, cancellationToken);
        return ServiceResult<AgentTokenDto>.Success(new AgentTokenDto(server.Id, token),
            replaced ? "Yeni token oluşturuldu; önceki token artık kabul edilmiyor." : "Agent token'ı oluşturuldu.");
    }

    public async Task<ServiceResult> RevokeTokenAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (server.AgentTokenHash is null)
            return ServiceResult.Failure("Bu sunucu için agent token'ı yok.");

        server.AgentTokenHash = null;
        server.AgentTokenCreatedAt = null;
        await _serverRepository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.AgentTokenRevoke, server, null, cancellationToken);
        return ServiceResult.Success("Token iptal edildi; agent raporları artık kabul edilmiyor.");
    }

    public async Task<AgentReportOutcome> ReportAsync(string? token, string? agentVersion, string output, CancellationToken cancellationToken = default)
    {
        if (!AgentTokens.IsWellFormed(token))
            return new AgentReportOutcome(AgentReportStatus.InvalidToken, "Geçersiz agent token'ı.");

        var server = await _serverRepository.GetByAgentTokenHashAsync(AgentTokens.Hash(token!), cancellationToken);
        if (server is null)
            return new AgentReportOutcome(AgentReportStatus.InvalidToken, "Geçersiz agent token'ı.");

        if (!server.MonitoringEnabled)
            return new AgentReportOutcome(AgentReportStatus.MonitoringDisabled, "Bu sunucu için izleme kapalı.");

        var now = UtcNow;
        if (server.AgentLastSeenAt is { } lastSeen && now - lastSeen < TimeSpan.FromSeconds(AgentRules.MinReportIntervalSeconds))
            return new AgentReportOutcome(AgentReportStatus.TooFrequent, $"Raporlar arasında en az {AgentRules.MinReportIntervalSeconds} saniye olmalı.");

        SystemMetricsSnapshot snapshot;
        try
        {
            snapshot = _parser.Parse(output, now);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning("Agent raporu ayrıştırılamadı. ServerId: {ServerId}, Error: {Error}", server.Id, ex.Message);
            return new AgentReportOutcome(AgentReportStatus.InvalidReport, "Rapor okunamadı. Sunucunun Linux olduğundan emin olun.");
        }

        server.AgentLastSeenAt = now;
        server.AgentVersion = AgentRules.NormalizeVersion(agentVersion);
        await _monitoringService.RecordAgentReportAsync(server.Id, new MetricsCollectionResult
        {
            IsSuccess = true,
            Message = "Agent raporu alındı.",
            Snapshot = snapshot
        }, cancellationToken);
        await _serverRepository.SaveChangesAsync(cancellationToken);

        return new AgentReportOutcome(AgentReportStatus.Accepted, "Rapor alındı.");
    }

    public string BuildInstallScript() => AgentInstallScript.Build(_parser.CollectionScript);

    public async Task<int> MarkSilentAgentsOfflineAsync(CancellationToken cancellationToken = default)
    {
        var silent = await _serverRepository.GetSilentAgentServersAsync(UtcNow - AgentRules.SilentAfter, cancellationToken);
        foreach (var server in silent)
        {
            var minutes = (int)Math.Floor((UtcNow - server.AgentLastSeenAt!.Value).TotalMinutes);
            await _monitoringService.RecordAgentReportAsync(server.Id, new MetricsCollectionResult
            {
                IsSuccess = false,
                Message = $"Agent {minutes} dakikadır rapor göndermiyor."
            }, cancellationToken);
        }

        return silent.Count;
    }

    private AgentStatusDto ToStatus(Server server) =>
        new(server.Id, server.Name, server.AgentTokenHash is not null, server.AgentTokenCreatedAt, server.AgentLastSeenAt,
            server.AgentVersion, server.AgentTokenHash is not null && AgentRules.IsActive(server.AgentLastSeenAt, UtcNow), server.MonitoringEnabled);

    private Task AuditAsync(string action, Server server, string? details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.Server, server.Id.ToString(), server.Name, details), cancellationToken);
}
