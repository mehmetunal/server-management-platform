using System.Threading.RateLimiting;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Framework.RateLimiting;

namespace ServerManager.Web.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    private const string TooManyRequestsMessage = "Çok fazla istek gönderildi. Lütfen biraz bekleyip tekrar deneyin.";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicies.Login, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options
                .AddPerUserPolicy(RateLimitPolicies.ConnectionTest, 10)
                .AddPerUserPolicy(RateLimitPolicies.MetricsCollect, 10)
                .AddPerUserPolicy(RateLimitPolicies.DockerAction, 30)
                .AddPerUserPolicy(RateLimitPolicies.FileAction, 60)
                .AddPerUserPolicy(RateLimitPolicies.DeploymentAction, 20)
                .AddPerUserPolicy(RateLimitPolicies.DeploymentLookup, 60);

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.HttpContext.Request.WantsJson())
                {
                    await response.WriteAsJsonAsync(
                        ApiResponse<object>.Fail(TooManyRequestsMessage, StatusCodes.Status429TooManyRequests),
                        cancellationToken);
                    return;
                }

                response.ContentType = "text/plain; charset=utf-8";
                await response.WriteAsync(TooManyRequestsMessage, cancellationToken);
            };
        });

        return services;
    }
}
