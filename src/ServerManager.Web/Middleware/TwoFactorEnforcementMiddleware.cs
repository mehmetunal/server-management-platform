using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Web.Extensions;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Options;

namespace ServerManager.Web.Middleware;

/// <summary><see cref="TwoFactorOptions.Required"/> açıkken iki adımlı doğrulaması olmayan kullanıcıyı kurulum sayfasına yönlendirir.</summary>
public sealed class TwoFactorEnforcementMiddleware
{
    private const string SetupPath = "/Account/Security";

    // /api/v1 yalnızca API anahtarıyla çalışır; anahtar kendi kimlik bilgisidir (oturum açma ve 2FA kurulumu gerekmez).
    private static readonly string[] AllowedPrefixes = ["/Account", "/Error", HealthEndpoints.LivenessPath, "/api/v1"];

    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<TwoFactorOptions> _options;

    public TwoFactorEnforcementMiddleware(RequestDelegate next, IOptionsMonitor<TwoFactorOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.CurrentValue.Required
            || context.User.Identity?.IsAuthenticated != true
            || context.User.HasClaim(AppClaimTypes.TwoFactorEnabled, "1")
            || AllowedPrefixes.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        if (context.Request.WantsJson())
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                "Devam etmek için Hesabım sayfasından iki adımlı doğrulamayı açın.", StatusCodes.Status403Forbidden));
            return;
        }

        context.Response.Redirect(SetupPath);
    }
}
