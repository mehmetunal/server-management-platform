using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Identity;

namespace ServerManager.Web.Terminal;

/// <summary>
/// SignalR bağlantısındaki kimlik bağlantı süresince sabit kalır; cookie'nin security stamp doğrulaması hub çağrılarına
/// uygulanmaz. Terminal girdileri bu yüzden kullanıcının hâlâ aktif, kilitsiz ve aynı security stamp'e sahip olduğunu
/// kısa süreli önbellekle yeniden doğrular.
/// </summary>
public sealed class TerminalAccessValidator
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _securityStampClaimType;
    private readonly ILogger<TerminalAccessValidator> _logger;

    public TerminalAccessValidator(
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> identityOptions,
        ILogger<TerminalAccessValidator> logger)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _securityStampClaimType = identityOptions.Value.ClaimsIdentity.SecurityStampClaimType;
        _logger = logger;
    }

    public static string RequiredPermission(TerminalSessionKind kind) => kind switch
    {
        TerminalSessionKind.Container => Permissions.DockerTerminal,
        TerminalSessionKind.ServiceConsole => Permissions.ServicesConsole,
        _ => Permissions.TerminalExecute
    };

    /// <summary>Oturum türüne göre gereken yetki (sunucu: terminal.execute, container: docker.terminal).</summary>
    public static bool HasPermission(ClaimsPrincipal? principal, TerminalSessionKind kind) =>
        principal?.Identity?.IsAuthenticated == true
        && (principal.IsInRole(Roles.SuperAdmin) || principal.HasClaim(Permissions.ClaimType, RequiredPermission(kind)));

    /// <returns>
    /// Kullanıcı geçerliyse true, pasif/kilitli/silinmiş ya da security stamp değişmişse false;
    /// veritabanına ulaşılamadıysa null (karar çağırana bırakılır, sonuç önbelleğe yazılmaz).
    /// </returns>
    public async Task<bool?> IsUserStillValidAsync(ClaimsPrincipal? principal, CancellationToken cancellationToken = default)
    {
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = principal?.FindFirstValue(_securityStampClaimType);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(stamp))
            return false;

        var key = CacheKey(userId);
        if (_cache.TryGetValue(key, out CachedResult? cached) && cached is not null && cached.Stamp == stamp)
            return cached.Valid;

        bool valid;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            valid = user is not null
                    && user.IsActive
                    && !(user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
                    && string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Terminal kullanıcı doğrulaması yapılamadı. UserId: {UserId}", userId);
            return null;
        }

        _cache.Set(key, new CachedResult(stamp, valid), CacheDuration);
        return valid;
    }

    /// <summary>Oturumlar iptal edildiğinde önbellekteki eski "geçerli" sonucun kullanılmaması için.</summary>
    public void Invalidate(string userId) => _cache.Remove(CacheKey(userId));

    private static string CacheKey(string userId) => $"terminal-access:{userId}";

    private sealed record CachedResult(string Stamp, bool Valid);
}
