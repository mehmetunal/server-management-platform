using System.Net;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.ApiKeys;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>Kişisel API anahtarları: oluşturma (bir kez gösterilir), listeleme, iptal ve istek anında doğrulama.</summary>
public interface IApiKeyService
{
    /// <summary>Geçerli kullanıcının anahtarları.</summary>
    Task<IReadOnlyList<ApiKeyListItemDto>> GetMineAsync(CancellationToken cancellationToken = default);

    /// <summary>Tüm kullanıcıların anahtarları (yönetici sayfası).</summary>
    Task<IReadOnlyList<ApiKeyListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Geçerli kullanıcının anahtara verebileceği izinler (kendi o anki izinleri).</summary>
    Task<IReadOnlyList<PermissionInfo>> GetGrantableScopesAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<ApiKeyCreatedDto>> CreateAsync(CreateApiKeyDto dto, CancellationToken cancellationToken = default);

    /// <summary>Kendi anahtarını iptal eder; <paramref name="asAdmin"/> ise herhangi bir kullanıcının anahtarını.</summary>
    Task<ServiceResult> RevokeAsync(Guid id, bool asAdmin, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>Authorization: Bearer smk_…</c> değerini doğrular: biçim, özet (sabit zamanlı), iptal, süre, IP izin listesi,
    /// kullanıcının aktif ve kilitsiz olması. Başarıda son kullanım bilgisi güncellenir.
    /// </summary>
    Task<ApiKeyAuthResult> AuthenticateAsync(string token, IPAddress? remoteIp, CancellationToken cancellationToken = default);
}
