using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.DTOs.Terminal;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class TerminalService : ITerminalService
{
    public const int SessionPageSize = 15;
    public const int RecentCommandCount = 50;
    public const int MaxCommandLength = 2000;

    private readonly IServerConnectionProvider _connectionProvider;
    private readonly ITerminalSessionFactory _sessionFactory;
    private readonly IDockerService _dockerService;
    private readonly ITerminalLogRepository _terminalLogRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TerminalService> _logger;

    public TerminalService(
        IServerConnectionProvider connectionProvider,
        ITerminalSessionFactory sessionFactory,
        IDockerService dockerService,
        ITerminalLogRepository terminalLogRepository,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        ILogger<TerminalService> logger)
    {
        _connectionProvider = connectionProvider;
        _sessionFactory = sessionFactory;
        _dockerService = dockerService;
        _terminalLogRepository = terminalLogRepository;
        _auditLogService = auditLogService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ServiceResult<TerminalHandle>> OpenServerShellAsync(
        Guid serverId,
        int columns,
        int rows,
        TerminalActor actor,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<TerminalHandle>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var request = new TerminalOpenRequest
        {
            Context = connection.Data!.Context,
            Columns = Math.Clamp(columns, 20, 500),
            Rows = Math.Clamp(rows, 5, 200)
        };
        var result = await _sessionFactory.OpenAsync(request, sink, cancellationToken);

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.TerminalOpen,
            AuditEntityTypes.Server,
            serverId.ToString(),
            connection.Data.ServerName,
            result.IsSuccess ? "Sunucu kabuğu" : $"Sunucu kabuğu | Hata: {result.Message}",
            result.IsSuccess,
            actor.UserName,
            actor.UserId,
            actor.IpAddress), cancellationToken);

        if (!result.IsSuccess)
            return ServiceResult<TerminalHandle>.Failure(result.Message ?? "Terminal açılamadı.", result.ErrorType);

        var handle = new TerminalHandle
        {
            ServerId = serverId,
            ServerName = connection.Data.ServerName,
            Kind = TerminalSessionKind.Server,
            Session = result.Data!
        };
        await SaveSessionAsync(handle, actor, cancellationToken);
        return ServiceResult<TerminalHandle>.Success(handle);
    }

    public async Task<ServiceResult<TerminalHandle>> OpenContainerShellAsync(
        Guid serverId,
        string container,
        int columns,
        int rows,
        TerminalActor actor,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default)
    {
        var result = await _dockerService.OpenTerminalAsync(serverId, container, columns, rows, sink, cancellationToken);
        if (result.IsSuccess)
            await SaveSessionAsync(result.Data!, actor, cancellationToken);

        return result;
    }

    public async Task CompleteSessionAsync(TerminalHandle handle, TerminalActor actor, string? reason, CancellationToken cancellationToken = default)
    {
        var endedAt = _timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await _terminalLogRepository.CloseSessionAsync(actor.SessionId, endedAt, TextHelper.Truncate(reason, 256), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terminal oturum kaydı kapatılamadı. SessionId: {SessionId}", actor.SessionId);
        }

        var minutes = (int)Math.Max(0, (endedAt - handle.Session.StartedAt).TotalMinutes);
        var details = handle.Kind == TerminalSessionKind.Container
            ? $"Container: {handle.Container}, süre: {minutes} dk"
            : $"Sunucu kabuğu, süre: {minutes} dk";
        if (!string.IsNullOrEmpty(reason))
            details += $", neden: {reason}";

        await _auditLogService.LogAsync(new AuditEntry(
            handle.Kind == TerminalSessionKind.Container ? AuditActions.DockerTerminalClose : AuditActions.TerminalClose,
            AuditEntityTypes.Server,
            handle.ServerId.ToString(),
            handle.ServerName,
            details,
            true,
            actor.UserName,
            actor.UserId,
            actor.IpAddress), cancellationToken);
    }

    public async Task RecordCommandsAsync(IReadOnlyList<TerminalCommandEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return;

        await _terminalLogRepository.AddCommandsAsync(entries.Select(e => new TerminalCommandLog
        {
            SessionId = e.SessionId,
            ExecutedAt = e.ExecutedAt,
            CommandText = TextHelper.Truncate(e.CommandText, MaxCommandLength) ?? string.Empty,
            IsApproximate = e.IsApproximate,
            Status = e.Status,
            MatchedRule = TextHelper.Truncate(e.MatchedRule, 256)
        }), cancellationToken);
        await _terminalLogRepository.SaveChangesAsync(cancellationToken);

        var counts = entries
            .Where(e => e.Status is TerminalCommandStatus.Executed or TerminalCommandStatus.Confirmed)
            .GroupBy(e => e.SessionId)
            .ToDictionary(g => g.Key, g => g.Count());
        await _terminalLogRepository.IncrementCommandCountsAsync(counts, cancellationToken);

        foreach (var entry in entries.Where(e => e.MatchedRule is not null))
        {
            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.TerminalDangerousCommand,
                AuditEntityTypes.Server,
                entry.ServerId.ToString(),
                entry.ServerName,
                $"{DangerousStatusText(entry.Status)} | Kural: {entry.MatchedRule} | Komut: {entry.CommandText}",
                entry.Status is TerminalCommandStatus.Executed or TerminalCommandStatus.Confirmed,
                entry.Actor.UserName,
                entry.Actor.UserId,
                entry.Actor.IpAddress), cancellationToken);
        }
    }

    public Task<int> CloseOpenSessionsAsync(DateTime startedBefore, string reason, CancellationToken cancellationToken = default) =>
        _terminalLogRepository.CloseOpenSessionsAsync(startedBefore, _timeProvider.GetUtcNow().UtcDateTime, reason, cancellationToken);

    public async Task<PagedResult<TerminalSessionDto>> GetSessionsAsync(Guid serverId, string? userId, int page, CancellationToken cancellationToken = default)
    {
        var sessions = await _terminalLogRepository.GetSessionsAsync(serverId, userId, page, SessionPageSize, cancellationToken);
        return sessions.Map(s => s.ToDto());
    }

    public async Task<ServiceResult<TerminalSessionDetailsDto>> GetSessionAsync(Guid serverId, Guid sessionId, string? userId, CancellationToken cancellationToken = default)
    {
        var session = await _terminalLogRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.ServerId != serverId || (userId is not null && session.UserId != userId))
            return ServiceResult<TerminalSessionDetailsDto>.NotFound("Terminal oturumu bulunamadı.");

        var commands = await _terminalLogRepository.GetCommandsAsync(sessionId, cancellationToken);
        return ServiceResult<TerminalSessionDetailsDto>.Success(new TerminalSessionDetailsDto
        {
            Session = session.ToDto(),
            Commands = commands.Select(c => c.ToDto()).ToList()
        });
    }

    public Task<IReadOnlyList<string>> GetRecentCommandsAsync(Guid serverId, string userId, CancellationToken cancellationToken = default) =>
        _terminalLogRepository.GetRecentCommandTextsAsync(serverId, userId, RecentCommandCount, cancellationToken);

    private async Task SaveSessionAsync(TerminalHandle handle, TerminalActor actor, CancellationToken cancellationToken)
    {
        try
        {
            await _terminalLogRepository.AddAsync(new TerminalSessionLog
            {
                Id = actor.SessionId,
                ServerId = handle.ServerId,
                ServerName = TextHelper.Truncate(handle.ServerName, 256) ?? string.Empty,
                UserId = TextHelper.Truncate(actor.UserId, 64),
                UserName = TextHelper.Truncate(actor.UserName, 256),
                IpAddress = TextHelper.Truncate(actor.IpAddress, 45),
                Kind = handle.Kind,
                Container = TextHelper.Truncate(handle.Container, 255),
                StartedAt = handle.Session.StartedAt
            }, cancellationToken);
            await _terminalLogRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terminal oturum kaydı yazılamadı. ServerId: {ServerId}", handle.ServerId);
        }
    }

    private static string DangerousStatusText(TerminalCommandStatus status) => status switch
    {
        TerminalCommandStatus.Confirmed => "Onaylandı",
        TerminalCommandStatus.Cancelled => "İptal edildi",
        TerminalCommandStatus.Blocked => "Engellendi",
        _ => "Uyarı ile çalıştırıldı"
    };
}
