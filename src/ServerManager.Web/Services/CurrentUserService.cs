using System.Security.Claims;
using ServerManager.Application.Interfaces;

namespace ServerManager.Web.Services;

public class CurrentUserService : ICurrentUserService
{
    /// <summary>
    /// HTTP isteği bittikten sonra arka planda süren işlemlerin (ör. servis kurulumu sonrası yedek işi oluşturma) işlemi başlatan
    /// kullanıcı adına kayıt ve audit yazması için; yalnızca <see cref="RunAs"/> ile açılan akışta geçerlidir.
    /// </summary>
    private static readonly AsyncLocal<BackgroundUser?> Override = new();

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    public string? UserId => Override.Value?.UserId ?? Context?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => Override.Value?.UserName ?? Context?.User.Identity?.Name;

    public string? IpAddress => Override.Value?.IpAddress ?? Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Override.Value is null ? Context?.Request.Headers.UserAgent.ToString() : null;

    public bool IsAuthenticated => Override.Value is not null || Context?.User.Identity?.IsAuthenticated == true;

    /// <summary>Dönen nesne dispose edilene kadar bu async akışta kullanıcı bilgisi verilen değerlerdir.</summary>
    public static IDisposable RunAs(string? userId, string? userName, string? ipAddress)
    {
        var previous = Override.Value;
        Override.Value = new BackgroundUser(userId, userName, ipAddress);
        return new Restore(previous);
    }

    private sealed record BackgroundUser(string? UserId, string? UserName, string? IpAddress);

    private sealed class Restore(BackgroundUser? previous) : IDisposable
    {
        public void Dispose() => Override.Value = previous;
    }
}
