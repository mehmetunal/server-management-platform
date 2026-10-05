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

    /// <summary>
    /// Önceki imzayı okuma ve yeni kaydı ekleme tek adım olmalı; aksi halde zincir çatallanır. Asıl koruma veritabanı
    /// kilididir (birden fazla örnek); bu semafor yalnızca aynı süreçteki yazıcıları veritabanına gitmeden sıraya sokar.
    /// </summary>
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

            await _auditLogRepository.RunInChainLockAsync(ct => AppendAsync(log, ct), cancellationToken);
        }
        catch (Exception ex)
        {
            // Paylaşılan DbContext'te Added olarak kalırsa sonraki SaveChanges kaydı eski zincir imzasıyla ekler.
            _auditLogRepository.Detach(log);
            _logger.LogError(ex, "Audit log yazılamadı. Action: {Action}, EntityId: {EntityId}", entry.Action, entry.EntityId);
        }
        finally
        {
            if (locked)
                ChainLock.Release();
        }
    }

    public async Task EnsureChainAnchorAsync(CancellationToken cancellationToken = default)
    {
        AuditChainSummary? created = null;
        await ChainLock.WaitAsync(cancellationToken);
        try
        {
            await _auditLogRepository.RunInChainLockAsync(async ct =>
            {
                created = null;
                if (await _auditLogRepository.GetChainAnchorAsync(ct) is not null)
                    return;

                if (await _auditLogRepository.GetChainSummaryAsync(ct) is { } summary)
                {
                    await _auditLogRepository.SaveChainAnchorAsync(AuditChainVerifier.Create(summary, _chainSigner, DateTime.UtcNow), ct);
                    created = summary;
                }
            }, cancellationToken);
        }
        finally
        {
            ChainLock.Release();
        }

        if (created is null)
            return;

        // Çapa çapa öncesi sürümden yükseltmede bir kez oluşur. Sonradan yeniden oluşması çapanın silindiğini gösterir;
        // bu yüzden olay zincirin kendisine yazılır ve iz silinemez.
        _logger.LogWarning("Audit zincir çapası mevcut {Count} imzalı kayıttan oluşturuldu (son kayıt #{LastId}).", created.SignedCount, created.LastSignedId);
        await LogAsync(new AuditEntry(
            AuditActions.AuditChainAnchorCreate,
            AuditEntityTypes.AuditLog,
            created.LastSignedId.ToString(CultureInfo.InvariantCulture),
            Details: $"Çapa mevcut zincirden oluşturuldu. İmzalı kayıt: {created.SignedCount.ToString(CultureInfo.InvariantCulture)}, ilk imzalı kayıt: #{created.FirstSignedId.ToString(CultureInfo.InvariantCulture)}",
            UserNameOverride: "system"), cancellationToken);
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
        // Çapa kayıtlardan önce okunur; doğrulama sırasında eklenen kayıtlar çapanın ötesinde kalır.
        var anchor = await _auditLogRepository.GetChainAnchorAsync(cancellationToken);
        var result = await AuditChainVerifier.VerifyAsync(
            ReadChainAsync(cancellationToken), _chainSigner, anchor, anchorExpected: true, cancellationToken);

        await LogAsync(new AuditEntry(
            AuditActions.AuditVerify,
            AuditEntityTypes.AuditLog,
            result.BrokenAtId?.ToString(CultureInfo.InvariantCulture),
            Details: $"{result.Message} Kontrol edilen: {result.CheckedCount.ToString(CultureInfo.InvariantCulture)}, imzasız: {result.UnsignedCount.ToString(CultureInfo.InvariantCulture)}",
            IsSuccess: result.IsValid), cancellationToken);

        return result;
    }

    /// <summary>Zincir kilidi altında çalışır; geçici hatada yeniden denenebileceği için her adım baştan kurulur.</summary>
    private async Task AppendAsync(AuditLog log, CancellationToken cancellationToken)
    {
        var anchor = await _auditLogRepository.GetChainAnchorAsync(cancellationToken);

        // İmzalı kayıt varken çapa yoksa çapa silinmiştir (yükseltme durumunu açılıştaki EnsureChainAnchorAsync karşılar).
        // Burada sessizce yeniden oluşturulmaz; doğrulama eksik çapayı raporlar.
        var updateAnchor = anchor is not null || await _auditLogRepository.GetChainSummaryAsync(cancellationToken) is null;
        if (!updateAnchor)
            _logger.LogWarning("Audit zincir çapası bulunamadı; kayıt çapa güncellenmeden yazılıyor.");

        var previousHash = await _auditLogRepository.GetLastChainHashAsync(cancellationToken);
        log.ChainHash = _chainSigner.Sign(AuditChainFormat.Canonicalize(previousHash, log));

        _auditLogRepository.Detach(log);
        log.Id = 0;
        await _auditLogRepository.AddAsync(log, cancellationToken);
        await _auditLogRepository.SaveChangesAsync(cancellationToken);

        if (updateAnchor)
            await _auditLogRepository.SaveChainAnchorAsync(AuditChainVerifier.Advance(anchor, log, _chainSigner, DateTime.UtcNow), cancellationToken);
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
