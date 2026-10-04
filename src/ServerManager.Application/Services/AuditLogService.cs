using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class AuditLogService : IAuditLogService
{
    public const int MaxExportRows = 50_000;
    private const int VerifyBatchSize = 2_000;
    private const int TopActionCount = 5;

    /// <summary>Önceki imzayı okuma ve yeni kaydı ekleme tek adım olmalı; aksi halde zincir çatallanır.</summary>
    private static readonly SemaphoreSlim ChainLock = new(1, 1);

    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditChainSigner _chainSigner;
    private readonly AuditActionCatalog _actionCatalog;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        IAuditLogRepository auditLogRepository,
        ICurrentUserService currentUser,
        IAuditChainSigner chainSigner,
        AuditActionCatalog actionCatalog,
        ILogger<AuditLogService> logger)
    {
        _auditLogRepository = auditLogRepository;
        _currentUser = currentUser;
        _chainSigner = chainSigner;
        _actionCatalog = actionCatalog;
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
            CreatedAt = AuditChainFormat.NormalizeTimestamp(DateTime.UtcNow)
        };

        var locked = false;
        try
        {
            await ChainLock.WaitAsync(cancellationToken);
            locked = true;

            var previousHash = await _auditLogRepository.GetLastChainHashAsync(cancellationToken);
            log.ChainHash = _chainSigner.Sign(AuditChainFormat.Canonicalize(previousHash, log));

            await _auditLogRepository.AddAsync(log, cancellationToken);
            await _auditLogRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit log yazılamadı. Action: {Action}, EntityId: {EntityId}", entry.Action, entry.EntityId);
        }
        finally
        {
            if (locked)
                ChainLock.Release();
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

    public async Task<AuditLogDto?> GetAsync(long id, CancellationToken cancellationToken = default) =>
        (await _auditLogRepository.GetDetailsAsync(id, cancellationToken))?.ToDto();

    public Task<AuditStatsDto> GetStatsAsync(TimeSpan window, CancellationToken cancellationToken = default) =>
        _auditLogRepository.GetStatsAsync(DateTime.UtcNow - window, AuditActions.LoginFailed, TopActionCount, cancellationToken);

    public Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default) =>
        _auditLogRepository.GetEntityTypesAsync(cancellationToken);

    public async Task<AuditExportDto> ExportAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _auditLogRepository.ExportAsync(filter, MaxExportRows, cancellationToken);
        var result = new AuditExportDto { Items = items.Select(l => l.ToDto()).ToList(), TotalCount = totalCount };

        await LogAsync(new AuditEntry(
            AuditActions.AuditExport,
            AuditEntityTypes.AuditLog,
            Details: $"{result.Items.Count.ToString(CultureInfo.InvariantCulture)} kayıt dışa aktarıldı"
                + (result.Truncated ? $" (toplam {totalCount.ToString(CultureInfo.InvariantCulture)}, sınır {MaxExportRows.ToString(CultureInfo.InvariantCulture)})" : string.Empty)
                + DescribeFilter(filter)), cancellationToken);

        return result;
    }

    public async Task<AuditChainVerificationDto> VerifyChainAsync(CancellationToken cancellationToken = default)
    {
        var result = await AuditChainVerifier.VerifyAsync(ReadChainAsync(cancellationToken), _chainSigner, cancellationToken);

        await LogAsync(new AuditEntry(
            AuditActions.AuditVerify,
            AuditEntityTypes.AuditLog,
            result.BrokenAtId?.ToString(CultureInfo.InvariantCulture),
            Details: $"{result.Message} Kontrol edilen: {result.CheckedCount.ToString(CultureInfo.InvariantCulture)}, imzasız: {result.UnsignedCount.ToString(CultureInfo.InvariantCulture)}",
            IsSuccess: result.IsValid), cancellationToken);

        return result;
    }

    private async IAsyncEnumerable<AuditLog> ReadChainAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long lastId = 0;
        while (true)
        {
            var batch = await _auditLogRepository.GetChainBatchAsync(lastId, VerifyBatchSize, cancellationToken);
            foreach (var log in batch)
                yield return log;

            if (batch.Count < VerifyBatchSize)
                yield break;

            lastId = batch[^1].Id;
        }
    }

    private string DescribeFilter(AuditLogFilterDto filter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Search)) parts.Add($"arama: {filter.Search.Trim()}");
        if (!string.IsNullOrWhiteSpace(filter.Action)) parts.Add($"işlem: {_actionCatalog.DisplayName(filter.Action)}");
        if (filter.IsSuccess.HasValue) parts.Add(filter.IsSuccess.Value ? "başarılı" : "başarısız");
        if (!string.IsNullOrWhiteSpace(filter.User)) parts.Add($"kullanıcı: {filter.User.Trim()}");
        if (!string.IsNullOrWhiteSpace(filter.Ip)) parts.Add($"IP: {filter.Ip.Trim()}");
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) parts.Add($"hedef türü: {filter.EntityType}");
        if (filter.From.HasValue) parts.Add($"başlangıç: {filter.From.Value:yyyy-MM-dd}");
        if (filter.To.HasValue) parts.Add($"bitiş: {filter.To.Value:yyyy-MM-dd}");
        return parts.Count == 0 ? string.Empty : "; filtre: " + string.Join(", ", parts);
    }
}
