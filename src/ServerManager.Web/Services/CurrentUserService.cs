using System.Security.Claims;
using ServerManager.Application.Interfaces;

namespace ServerManager.Web.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    public string? UserId => Context?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => Context?.User.Identity?.Name;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();

    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated == true;
}
