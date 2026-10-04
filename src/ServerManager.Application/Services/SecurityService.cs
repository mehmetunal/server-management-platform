using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Security;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Security;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public sealed class SecurityService : ISecurityService
{
    public const int HistorySize = 15;

    private const string SystemUserName = "Sistem";

    private static readonly ConcurrentDictionary<Guid, byte> RunningServers = new();

    private readonly IServerRepository _serverRepository;
    private readonly ISecurityScanRepository _scanRepository;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly ISecurityScanner _scanner;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly SecurityScanOptions _options;
    private readonly ILogger<SecurityService> _logger;

    public SecurityService(
        IServerRepository serverRepository,
        ISecurityScanRepository scanRepository,
        IServerConnectionProvider connectionProvider,
        ISecurityScanner scanner,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IOptions<SecurityScanOptions> options,
        ILogger<SecurityService> logger)
    {
        _serverRepository = serverRepository;
        _scanRepository = scanRepository;
        _connectionProvider = connectionProvider;
        _scanner = scanner;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SecurityOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.FindAsync(_ => true, cancellationToken);
        var latest = await _scanRepository.GetLatestPerServerAsync(cancellationToken);
        var completed = await _scanRepository.GetLatestCompletedPerServerAsync(cancellationToken);

        var items = servers
            .OrderBy(s => s.Name)
            .Select(s => new SecurityServerSummaryDto
            {
                ServerId = s.Id,
                ServerName = s.Name,
                Host = string.IsNullOrEmpty(s.IpAddress) ? s.Hostname : s.IpAddress,
                HostKeyVerified = !string.IsNullOrEmpty(s.HostKeyFingerprint),
                LatestScan = latest.TryGetValue(s.Id, out var scan) ? ToSummary(scan) : null
            })
            .ToList();

        var scores = completed.Where(c => c.Score.HasValue).Select(c => c.Score!.Value).ToList();
        return new SecurityOverviewDto
        {
            Servers = items,
            ScannedCount = completed.Count,
            AverageScore = scores.Count == 0 ? null : (int)Math.Round(scores.Average()),
            CriticalTotal = completed.Sum(c => c.CriticalCount),
            WarningTotal = completed.Sum(c => c.WarningCount),
            ServersWithCritical = completed.Count(c => c.CriticalCount > 0),
            ScanIntervalHours = _options.ScanIntervalHours
        };
    }

    public async Task<ServiceResult<SecurityServerReportDto>> GetServerReportAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult<SecurityServerReportDto>.NotFound("Sunucu bulunamadı.");

        var completed = await _scanRepository.GetLatestCompletedAsync(serverId, cancellationToken);
        var history = await _scanRepository.GetHistoryAsync(serverId, HistorySize, cancellationToken);
        var newest = history.FirstOrDefault();
        var latestFailure = newest is { Status: SecurityScanStatus.Failed } && (completed is null || newest.StartedAt > completed.StartedAt)
            ? ToSummary(newest)
            : null;

        return ServiceResult<SecurityServerReportDto>.Success(new SecurityServerReportDto
        {
            ServerId = server.Id,
            ServerName = server.Name,
            HostKeyVerified = !string.IsNullOrEmpty(server.HostKeyFingerprint),
            UseSudo = server.UseSudo,
            CompletedScan = completed is null ? null : ToSummary(completed),
            Report = SecurityReportSerializer.Deserialize(completed?.ReportJson),
            LatestFailure = latestFailure,
            IsRunning = RunningServers.ContainsKey(serverId) || newest?.Status == SecurityScanStatus.Running,
            History = history.Select(ToSummary).ToList()
        });
    }

    public async Task<ServiceResult<SecurityScanSummaryDto>> ScanAsync(Guid serverId, SecurityScanTrigger trigger, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult<SecurityScanSummaryDto>.NotFound("Sunucu bulunamadı.");

        if (!RunningServers.TryAdd(serverId, 0))
            return ServiceResult<SecurityScanSummaryDto>.Failure("Bu sunucuda tarama zaten sürüyor.", ServiceErrorType.Conflict);

        try
        {
            if (await _scanRepository.HasRunningAsync(serverId, cancellationToken))
                return ServiceResult<SecurityScanSummaryDto>.Failure("Bu sunucuda tarama zaten sürüyor.", ServiceErrorType.Conflict);

            var scan = new SecurityScan
            {
                Id = Guid.NewGuid(),
                ServerId = server.Id,
                ServerName = server.Name,
                Trigger = trigger,
                Status = SecurityScanStatus.Running,
                StartedAt = DateTime.UtcNow,
                UserName = trigger == SecurityScanTrigger.Manual ? _currentUser.UserName : SystemUserName
            };
            await _scanRepository.AddAsync(scan, cancellationToken);
            await _scanRepository.SaveChangesAsync(cancellationToken);

            try
            {
                await RunScanAsync(scan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                scan.Status = SecurityScanStatus.Failed;
                scan.FailureReason = "Tarama iptal edildi.";
                scan.CompletedAt = DateTime.UtcNow;
                await _scanRepository.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Güvenlik taraması beklenmeyen hatayla bitti. ServerId: {ServerId}", serverId);
                scan.Status = SecurityScanStatus.Failed;
                scan.FailureReason = "Tarama sırasında beklenmeyen bir hata oluştu.";
            }

            scan.CompletedAt = DateTime.UtcNow;
            await _scanRepository.SaveChangesAsync(CancellationToken.None);

            var succeeded = scan.Status == SecurityScanStatus.Completed;
            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.SecurityScan,
                AuditEntityTypes.Server,
                server.Id.ToString(),
                server.Name,
                succeeded
                    ? $"Puan: {scan.Score}, kritik: {scan.CriticalCount}, uyarı: {scan.WarningCount}, yetki: {(scan.IsPrivileged ? "root" : "kısıtlı")}, tetikleyen: {(trigger == SecurityScanTrigger.Manual ? "elle" : "zamanlayıcı")}"
                    : scan.FailureReason,
                succeeded,
                UserNameOverride: trigger == SecurityScanTrigger.Manual ? null : SystemUserName), CancellationToken.None);

            return succeeded
                ? ServiceResult<SecurityScanSummaryDto>.Success(ToSummary(scan), $"Tarama tamamlandı. Puan: {scan.Score}/100, kritik: {scan.CriticalCount}, uyarı: {scan.WarningCount}.")
                : ServiceResult<SecurityScanSummaryDto>.Failure(scan.FailureReason ?? "Tarama başarısız.");
        }
        finally
        {
            RunningServers.TryRemove(serverId, out _);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetDueServerIdsAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.FindAsync(s => s.HostKeyFingerprint != null, cancellationToken);
        var latest = await _scanRepository.GetLatestPerServerAsync(cancellationToken);
        var cutoff = DateTime.UtcNow - interval;
        return servers
            .Where(s => !latest.TryGetValue(s.Id, out var scan) || (scan.Status != SecurityScanStatus.Running && scan.StartedAt <= cutoff))
            .OrderBy(s => latest.TryGetValue(s.Id, out var scan) ? scan.StartedAt : DateTime.MinValue)
            .Select(s => s.Id)
            .ToList();
    }

    public Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default) =>
        _scanRepository.InterruptRunningAsync(DateTime.UtcNow, "Uygulama tarama sırasında yeniden başlatıldı.", cancellationToken);

    public Task<int> DeleteExpiredAsync(int retentionDays, int keepLatestPerServer, CancellationToken cancellationToken = default) =>
        _scanRepository.DeleteExpiredAsync(DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays)), Math.Max(1, keepLatestPerServer), cancellationToken);

    private async Task RunScanAsync(SecurityScan scan, CancellationToken cancellationToken)
    {
        var connection = await _connectionProvider.GetAsync(scan.ServerId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
        {
            scan.Status = SecurityScanStatus.Failed;
            scan.FailureReason = connection.Message ?? "Sunucu bağlantı bilgisi hazırlanamadı.";
            return;
        }

        var facts = await _scanner.CollectAsync(connection.Data.Context, cancellationToken);
        if (!facts.IsSuccess || facts.Data is null)
        {
            scan.Status = SecurityScanStatus.Failed;
            scan.FailureReason = facts.Message ?? "Tarama başarısız.";
            return;
        }

        var report = SecurityAnalyzer.Analyze(facts.Data);
        scan.Status = SecurityScanStatus.Completed;
        scan.Score = report.Score;
        scan.CriticalCount = report.CriticalCount;
        scan.WarningCount = report.WarningCount;
        scan.IsPrivileged = facts.Data.IsRoot;
        scan.ReportJson = SecurityReportSerializer.Serialize(report);
    }

    private static SecurityScanSummaryDto ToSummary(SecurityScan scan) => new()
    {
        Id = scan.Id,
        ServerId = scan.ServerId,
        ServerName = scan.ServerName,
        Trigger = scan.Trigger,
        Status = scan.Status,
        StartedAt = scan.StartedAt,
        CompletedAt = scan.CompletedAt,
        Score = scan.Score,
        CriticalCount = scan.CriticalCount,
        WarningCount = scan.WarningCount,
        IsPrivileged = scan.IsPrivileged,
        FailureReason = scan.FailureReason,
        UserName = scan.UserName
    };
}
