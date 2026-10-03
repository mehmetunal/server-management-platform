using System.Security.Cryptography;
using ServerManager.Web.Framework.Security;

namespace ServerManager.Web.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[CspNonceExtensions.ItemKey] = nonce;

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            // Eski Safari sürümleri WebSocket için 'self' eşleşmesini desteklemez; SignalR için host açıkça yazılır.
            var webSocketOrigin = context.Request.Host.HasValue
                ? $" {(context.Request.IsHttps ? "wss" : "ws")}://{context.Request.Host.Value}"
                : string.Empty;
            var sameOriginFraming = context.IsSameOriginFramingAllowed();
            var formActionOrigins = string.Concat(context.GetFormActionOrigins().Select(origin => " " + origin));
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = sameOriginFraming ? "SAMEORIGIN" : "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers.ContentSecurityPolicy =
                "default-src 'self'; " +
                $"script-src 'self' 'nonce-{nonce}'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                $"connect-src 'self'{webSocketOrigin}; " +
                "object-src 'none'; " +
                (sameOriginFraming ? "frame-ancestors 'self'; " : "frame-ancestors 'none'; ") +
                $"form-action 'self'{formActionOrigins}; " +
                "base-uri 'self'";
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
