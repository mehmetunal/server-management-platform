using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.ApiKeys;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.ApiKeys;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Identity;

public class ApiKeyService : IApiKeyService
{
    /// <summary>Son kullanım bilgisi en sık bu aralıkla yazılır (her istekte veritabanına yazılmaz).</summary>
    public static readonly TimeSpan LastUsedWriteInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// <c>api_key.use</c> audit kaydı anahtar başına en sık bu aralıkla (veya kaynak IP değişince) yazılır; audit log her API
    /// isteğiyle dolmaz. Anahtarla yapılan değişiklikler (deploy, yedek başlatma …) ilgili servislerin kendi audit kayıtlarına
    /// kullanıcı adına ayrıca yazılır.
    /// </summary>
    public static readonly TimeSpan UseAuditInterval = TimeSpan.FromHours(1);

    private const string NotFoundMessage = "API anahtarı bulunamadı.";

    private readonly ApplicationDbContext _db;
    private readonly IPermissionCatalog _catalog;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptionsMonitor<ApiKeyOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(
        ApplicationDbContext db,
        IPermissionCatalog catalog,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IOptionsMonitor<ApiKeyOptions> options,
        TimeProvider timeProvider,
        ILogger<ApiKeyService> logger)
    {
        _db = db;
        _catalog = catalog;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ApiKeyListItemDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return [];

        return await ListAsync(_db.ApiKeys.Where(k => k.UserId == userId), cancellationToken);
    }

    public Task<IReadOnlyList<ApiKeyListItemDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        ListAsync(_db.ApiKeys, cancellationToken);

    public async Task<IReadOnlyList<PermissionInfo>> GetGrantableScopesAsync(CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return [];

        var user = await UserPermissions.LoadAsync(_db, _catalog, userId, cancellationToken);
        return _catalog.All.Where(p => user.Permissions.Contains(p.Name)).ToList();
    }

    public async Task<ServiceResult<ApiKeyCreatedDto>> CreateAsync(CreateApiKeyDto dto, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
            return ServiceResult<ApiKeyCreatedDto>.Failure("API anahtarları bu kurulumda kapalı (ApiKeys:Enabled).", ServiceErrorType.Forbidden);

        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return ServiceResult<ApiKeyCreatedDto>.Failure("Oturum bulunamadı.", ServiceErrorType.Forbidden);

        var errors = new List<ServiceError>();
        var name = dto.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            errors.Add(new ServiceError(nameof(dto.Name), "Anahtar adı zorunludur."));
        else if (name.Length > ApiKeyRules.MaxNameLength)
            errors.Add(new ServiceError(nameof(dto.Name), $"Anahtar adı en fazla {ApiKeyRules.MaxNameLength} karakter olabilir."));

        var now = UtcNow;
        if (!ApiKeyRules.TryResolveExpiry(dto.LifetimeDays, options, now, out var expiresAt, out var expiryError))
            errors.Add(new ServiceError(nameof(dto.LifetimeDays), expiryError!));

        var user = await UserPermissions.LoadAsync(_db, _catalog, userId, cancellationToken);
        var scopes = (dto.Scopes ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToList();
        if (scopes.Count == 0)
            errors.Add(new ServiceError(nameof(dto.Scopes), "Anahtar için en az bir izin seçin."));
        var notOwned = scopes.Where(s => !user.Permissions.Contains(s)).ToList();
        if (notOwned.Count > 0)
            errors.Add(new ServiceError(nameof(dto.Scopes), "Anahtara yalnızca kendi izinlerinizi verebilirsiniz: " + string.Join(", ", notOwned)));

        if (!ApiKeyRules.TryParseAllowList(dto.AllowedIps, out var networks, out var ipError))
            errors.Add(new ServiceError(nameof(dto.AllowedIps), ipError!));

        if (errors.Count > 0)
            return ServiceResult<ApiKeyCreatedDto>.ValidationFailure(errors);

        var activeCount = await _db.ApiKeys.CountAsync(
            k => k.UserId == userId && k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > now), cancellationToken);
        if (activeCount >= options.MaxKeysPerUser)
            return ServiceResult<ApiKeyCreatedDto>.Failure(
                $"En fazla {options.MaxKeysPerUser} etkin anahtarınız olabilir. Kullanmadıklarınızı iptal edin.", ServiceErrorType.Conflict);

        var generated = ApiKeyToken.Generate();
        var key = new ApiKey
        {
            UserId = userId,
            Name = name,
            Prefix = generated.Prefix,
            KeyHash = generated.Hash,
            Scopes = ApiKeyRules.FormatScopes(scopes),
            AllowedIps = ApiKeyRules.FormatAllowList(networks),
            ExpiresAt = expiresAt,
            CreatedAt = now,
            CreatedBy = _currentUser.UserName
        };
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.ApiKeyCreate,
            AuditEntityTypes.ApiKey,
            key.Id.ToString(),
            key.Name,
            $"Önek: {key.Prefix}; {scopes.Count} izin; süre: {(expiresAt is null ? "süresiz" : expiresAt.Value.ToString("yyyy-MM-dd"))}"
            + (key.AllowedIps is null ? string.Empty : $"; IP: {key.AllowedIps}")), cancellationToken);

        return ServiceResult<ApiKeyCreatedDto>.Success(new ApiKeyCreatedDto(key.Id, key.Name, generated.Token, key.ExpiresAt),
            "API anahtarı oluşturuldu. Anahtarı şimdi kopyalayın; bir daha gösterilmez.");
    }

    public async Task<ServiceResult> RevokeAsync(Guid id, bool asAdmin, CancellationToken cancellationToken = default)
    {
        var key = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (key is null || (!asAdmin && !string.Equals(key.UserId.ToString(), _currentUser.UserId, StringComparison.OrdinalIgnoreCase)))
            return ServiceResult.NotFound(NotFoundMessage);

        if (key.RevokedAt is not null)
            return ServiceResult.Failure("Anahtar zaten iptal edilmiş.", ServiceErrorType.Conflict);

        key.RevokedAt = UtcNow;
        key.RevokedBy = _currentUser.UserName;
        key.UpdatedAt = key.RevokedAt;
        key.UpdatedBy = _currentUser.UserName;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.ApiKeyRevoke,
            AuditEntityTypes.ApiKey,
            key.Id.ToString(),
            key.Name,
            $"Önek: {key.Prefix}" + (asAdmin && !string.Equals(key.UserId.ToString(), _currentUser.UserId, StringComparison.OrdinalIgnoreCase) ? "; yönetici tarafından" : string.Empty)), cancellationToken);

        return ServiceResult.Success("API anahtarı iptal edildi.");
    }

    public async Task<ApiKeyAuthResult> AuthenticateAsync(string token, IPAddress? remoteIp, CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.Enabled)
            return ApiKeyAuthResult.Fail("API anahtarları bu kurulumda kapalı.");

        if (!ApiKeyToken.TryParse(token, out var prefix))
            return ApiKeyAuthResult.Fail("Geçersiz API anahtarı.");

        var key = await _db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Prefix == prefix, cancellationToken);
        if (key is null || !ApiKeyToken.Verify(token, key.KeyHash))
            return ApiKeyAuthResult.Fail("Geçersiz API anahtarı.");

        var now = UtcNow;
        if (key.RevokedAt is not null)
            return ApiKeyAuthResult.Fail("API anahtarı iptal edilmiş.");

        if (ApiKeyRules.IsExpired(key.ExpiresAt, now))
            return ApiKeyAuthResult.Fail("API anahtarının süresi dolmuş.");

        if (!ApiKeyRules.TryParseAllowList(key.AllowedIps, out var networks, out _) || !ApiKeyRules.IsIpAllowed(networks, remoteIp))
            return ApiKeyAuthResult.Fail("API anahtarı bu IP adresinden kullanılamaz.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == key.UserId, cancellationToken);
        if (user is null || !user.IsActive || (user.LockoutEnd.HasValue && user.LockoutEnd.Value > _timeProvider.GetUtcNow()))
            return ApiKeyAuthResult.Fail("Anahtarın sahibi olan hesap pasif veya kilitli.");

        var userPermissions = await UserPermissions.LoadAsync(_db, _catalog, user.Id, cancellationToken);
        var effective = ApiKeyRules.EffectivePermissions(ApiKeyRules.ParseScopes(key.Scopes), userPermissions.Permissions);

        await RecordUseAsync(key, user, remoteIp, now, cancellationToken);

        return new ApiKeyAuthResult(true, null, key.Id, key.Name, user.Id, user.UserName ?? user.Email, effective);
    }

    private async Task RecordUseAsync(ApiKey key, ApplicationUser user, IPAddress? remoteIp, DateTime now, CancellationToken cancellationToken)
    {
        var ip = remoteIp is { IsIPv4MappedToIPv6: true } ? remoteIp.MapToIPv4().ToString() : remoteIp?.ToString();
        var ipChanged = !string.Equals(ip, key.LastUsedIp, StringComparison.Ordinal);
        var stale = key.LastUsedAt is null || now - key.LastUsedAt.Value >= LastUsedWriteInterval;
        if (!stale && !ipChanged)
            return;

        try
        {
            await _db.ApiKeys
                .Where(k => k.Id == key.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, now).SetProperty(k => k.LastUsedIp, ip), cancellationToken);

            if (key.LastUsedAt is null || now - key.LastUsedAt.Value >= UseAuditInterval || ipChanged)
            {
                await _auditLogService.LogAsync(new AuditEntry(
                    AuditActions.ApiKeyUse,
                    AuditEntityTypes.ApiKey,
                    key.Id.ToString(),
                    key.Name,
                    key.LastUsedAt is null ? "İlk kullanım" : ipChanged ? $"Yeni kaynak IP (önceki: {key.LastUsedIp})" : "Kullanım (saatlik özet)",
                    UserNameOverride: user.UserName ?? user.Email,
                    UserIdOverride: user.Id.ToString(),
                    IpAddressOverride: ip), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            // Son kullanım bilgisi yazılamazsa istek reddedilmez.
            _logger.LogWarning(ex, "API anahtarı son kullanım bilgisi yazılamadı: {KeyId}", key.Id);
        }
    }

    private async Task<IReadOnlyList<ApiKeyListItemDto>> ListAsync(IQueryable<ApiKey> query, CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var rows = await query.AsNoTracking()
            .Join(_db.Users.AsNoTracking(), k => k.UserId, u => u.Id, (k, u) => new { Key = k, u.Email })
            .OrderByDescending(r => r.Key.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ApiKeyListItemDto
            {
                Id = r.Key.Id,
                UserId = r.Key.UserId,
                UserEmail = r.Email,
                Name = r.Key.Name,
                DisplayKey = ApiKeyToken.Display(r.Key.Prefix),
                Scopes = ApiKeyRules.ParseScopes(r.Key.Scopes),
                AllowedIps = r.Key.AllowedIps,
                CreatedAt = r.Key.CreatedAt,
                ExpiresAt = r.Key.ExpiresAt,
                LastUsedAt = r.Key.LastUsedAt,
                LastUsedIp = r.Key.LastUsedIp,
                RevokedAt = r.Key.RevokedAt,
                RevokedBy = r.Key.RevokedBy,
                IsExpired = ApiKeyRules.IsExpired(r.Key.ExpiresAt, now)
            })
            .ToList();
    }
}
