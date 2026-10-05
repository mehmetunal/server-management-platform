using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ServerManager.Web.Tests.Infrastructure;

/// <summary>
/// TestServer istemci IP'si vermez; IP bazlı hız sınırları (giriş, agent raporu) tüm testlerde tek bölüme düşmesin diye
/// istemcinin test başlığındaki IP'yi bağlantıya yazar. Pipeline'ın en başında çalışır.
/// </summary>
public sealed class TestClientIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Client-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (IPAddress.TryParse(context.Request.Headers[HeaderName].ToString(), out var address))
                context.Connection.RemoteIpAddress = address;

            await nextMiddleware(context);
        });

        next(app);
    };
}
