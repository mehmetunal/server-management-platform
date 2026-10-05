using System.Net;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

/// <summary>Sağlık uçları kimlik doğrulaması olmadan çağrılabilmeli (Docker HEALTHCHECK, yük dengeleyici).</summary>
[Collection(WebCollection.Name)]
public sealed class HealthEndpointTests(ServerManagerWebFactory factory)
{
    [Fact]
    public async Task Liveness_endpoint_returns_200_anonymously()
    {
        using var client = factory.CreateTestClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_endpoint_exists_and_is_anonymous()
    {
        using var client = factory.CreateTestClient();

        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        // Veritabanı erişilebilir olduğundan 200 beklenir; 503 de "uç var" demektir. 404 veya giriş yönlendirmesi olmamalı.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable });
    }
}
