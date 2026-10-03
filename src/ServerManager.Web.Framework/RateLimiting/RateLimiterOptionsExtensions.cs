using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ServerManager.Web.Framework.RateLimiting;

public static class RateLimiterOptionsExtensions
{
    /// <summary>Kullanıcı başına (oturum yoksa IP başına) dakikalık sabit pencereli sınır.</summary>
    public static RateLimiterOptions AddPerUserPolicy(this RateLimiterOptions options, string policyName, int permitsPerMinute)
    {
        options.AddPolicy(policyName, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        return options;
    }
}
