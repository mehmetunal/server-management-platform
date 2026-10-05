using System.Net;
using Microsoft.Extensions.Options;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Plugin.Cloud.Scaleway;
using ServerManager.Plugin.Cloud.Tests.Fakes;

namespace ServerManager.Plugin.Cloud.Tests;

public class ScalewayCloudProviderTests
{
    private const string Token = "scw-test-secret";

    private readonly ScriptedHttpHandler _handler = new();
    private readonly ScalewayCloudProvider _provider;

    public ScalewayCloudProviderTests()
    {
        _provider = new ScalewayCloudProvider(new StubHttpClientFactory(_handler), Options.Create(new ScalewayOptions { ApiUrl = "https://scaleway.test/" }));
    }

    [Fact]
    public async Task ListServersAsync_SkipsClosedZonesAndReadsPrice()
    {
        _handler
            .Add("/instance/v1/zones/fr-par-1/servers", """{"servers":[{"id":"srv-1","name":"app-1","state":"running","commercial_type":"DEV1-S","zone":"fr-par-1","public_ip":{"address":"203.0.113.4"},"creation_date":"2026-09-01T10:00:00.000Z"}],"total_count":1}""")
            .Add("/instance/v1/zones/fr-par-1/products/servers", """{"servers":{"DEV1-S":{"hourly_price":0.01,"ncpus":2,"ram":2147483648}}}""");

        var result = await _provider.ListServersAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var server = Assert.Single(result.Data!);
        Assert.Equal("203.0.113.4", server.PublicIpv4);
        Assert.Equal(7.30m, server.MonthlyPrice);
        Assert.Equal("EUR", server.Currency);
        Assert.Equal(Token, _handler.Requests[0].Token);
        Assert.Contains(_handler.Requests, r => r.Uri.PathAndQuery.Contains("nl-ams-1", StringComparison.Ordinal) && r.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsDefaultProject()
    {
        _handler.Add("/account/v3/projects", """{"projects":[{"id":"p-1","name":"default"},{"id":"p-2","name":"other"}],"total_count":2}""");

        var result = await _provider.ValidateTokenAsync(Token, TestContext.Current.CancellationToken);

        Assert.Equal("Scaleway · default", result.Data);
    }

    [Fact]
    public async Task GetCatalogAsync_UsesRespondingZones()
    {
        _handler
            .Add("/instance/v1/zones/fr-par-1/products/servers", """{"servers":{"DEV1-S":{"hourly_price":0.01,"ncpus":2,"ram":2147483648,"arch":"x86_64"}}}""")
            .Add("/instance/v1/zones/fr-par-1/images", """{"images":[{"id":"img-1","name":"Ubuntu 24.04","arch":"x86_64","public":true},{"id":"img-2","name":"Private","public":false}],"total_count":2}""");

        var result = await _provider.GetCatalogAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["fr-par-1"], result.Data!.Regions.Select(r => r.Id));
        Assert.Equal(["DEV1-S"], result.Data.Sizes.Select(s => s.Id));
        Assert.Contains("2 GB RAM", result.Data.Sizes[0].Name);
        Assert.Equal(["Ubuntu 24.04"], result.Data.Images.Select(i => i.Id));
    }

    [Fact]
    public async Task CreateServerAsync_WritesCloudInitBeforePowerOn()
    {
        _handler
            .Add("/account/v3/projects", """{"projects":[{"id":"p-1","name":"default"}],"total_count":1}""")
            .Add("/instance/v1/zones/fr-par-1/images", """{"images":[{"id":"img-1","name":"Ubuntu 24.04","public":true}],"total_count":1}""")
            .Add("/instance/v1/zones/fr-par-1/servers", """{"server":{"id":"srv-9","name":"web-1","state":"stopped","commercial_type":"DEV1-S","zone":"fr-par-1"}}""")
            .Add("/instance/v1/zones/fr-par-1/servers/srv-9/user_data/cloud-init", "{}", HttpStatusCode.NoContent)
            .Add("/instance/v1/zones/fr-par-1/servers/srv-9/action", "{}", HttpStatusCode.Accepted);

        var result = await _provider.CreateServerAsync(Token, new CloudCreateServerRequest("web-1", "fr-par-1", "DEV1-S", "Ubuntu 24.04", "#cloud-config\n"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("srv-9", result.Data!.Server.ExternalId);
        Assert.Null(result.Data.RootPassword);
        Assert.Contains(_handler.Requests, r => r.Method == HttpMethod.Post && r.Body.Contains("\"image\":\"img-1\"", StringComparison.Ordinal));
        var userData = Assert.Single(_handler.Requests, r => r.Uri.AbsolutePath.EndsWith("/user_data/cloud-init", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Patch, userData.Method);
        Assert.Equal("#cloud-config\n", userData.Body);
        var action = _handler.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/action", StringComparison.Ordinal));
        Assert.True(_handler.Requests.IndexOf(userData) < _handler.Requests.IndexOf(action));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "API anahtarı geçersiz veya iptal edilmiş.")]
    [InlineData(HttpStatusCode.TooManyRequests, "Scaleway istek sınırına ulaşıldı; biraz sonra tekrar deneyin.")]
    [InlineData(HttpStatusCode.BadRequest, "Scaleway: commercial type is invalid")]
    public void ErrorMessage_MapsStatus(HttpStatusCode status, string expected)
    {
        Assert.Equal(expected, ScalewayCloudProvider.ErrorMessage(status, """{"message":"commercial type is invalid","type":"invalid_request"}"""));
    }
}
