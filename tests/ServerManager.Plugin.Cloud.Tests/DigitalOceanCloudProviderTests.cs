using System.Net;
using Microsoft.Extensions.Options;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Plugin.Cloud.DigitalOcean;
using ServerManager.Plugin.Cloud.Tests.Fakes;

namespace ServerManager.Plugin.Cloud.Tests;

public class DigitalOceanCloudProviderTests
{
    private const string Token = "dop_v1_test";

    private readonly ScriptedHttpHandler _handler = new();
    private readonly DigitalOceanCloudProvider _provider;

    public DigitalOceanCloudProviderTests()
    {
        _provider = new DigitalOceanCloudProvider(new StubHttpClientFactory(_handler), Options.Create(new DigitalOceanOptions { ApiUrl = "https://do.test/v2/" }));
    }

    private static string Droplet(int id, string name, string ip) => $$$"""
        {"id":{{{id}}},"name":"{{{name}}}","status":"active","created_at":"2026-09-01T10:00:00Z","size_slug":"s-1vcpu-1gb",
         "size":{"price_monthly":6.0},"region":{"slug":"fra1","name":"Frankfurt 1"},
         "networks":{"v4":[{"ip_address":"10.10.0.5","type":"private"},{"ip_address":"{{{ip}}}","type":"public"}]}}
        """;

    [Fact]
    public async Task ListServersAsync_ReadsPublicIpAndFollowsLinks()
    {
        _handler
            .Add("/v2/droplets?page=1", """{"droplets":[""" + Droplet(1, "app-1", "203.0.113.1") + """],"links":{"pages":{"next":"https://do.test/v2/droplets?page=2"}}}""")
            .Add("/v2/droplets?page=2", """{"droplets":[""" + Droplet(2, "app-2", "203.0.113.2") + """],"links":{}}""");

        var result = await _provider.ListServersAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["app-1", "app-2"], result.Data!.Select(s => s.Name));
        var first = result.Data![0];
        Assert.Equal("203.0.113.1", first.PublicIpv4);
        Assert.Equal("fra1", first.Region);
        Assert.Equal(6.00m, first.MonthlyPrice);
        Assert.Equal("USD", first.Currency);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsTeamName()
    {
        _handler.Add("/v2/account", """{"account":{"status":"active","team":{"name":"Ops"}}}""");

        var result = await _provider.ValidateTokenAsync(Token, TestContext.Current.CancellationToken);

        Assert.Equal("DigitalOcean · Ops", result.Data);
        Assert.Equal($"Bearer {Token}", _handler.Requests.Single().Authorization);
    }

    [Fact]
    public async Task GetCatalogAsync_FiltersUnavailable()
    {
        _handler
            .Add("/v2/regions", """{"regions":[{"slug":"fra1","name":"Frankfurt 1","available":true},{"slug":"old1","name":"Old","available":false}],"links":{}}""")
            .Add("/v2/sizes", """{"sizes":[{"slug":"s-2vcpu-2gb","vcpus":2,"memory":2048,"disk":60,"price_monthly":18.0,"available":true,"regions":["fra1"]},{"slug":"s-1vcpu-1gb","vcpus":1,"memory":1024,"disk":25,"price_monthly":6.0,"available":true,"regions":["fra1"]},{"slug":"gone","available":false,"regions":["fra1"]}],"links":{}}""")
            .Add("/v2/images", """{"images":[{"slug":"ubuntu-24-04-x64","distribution":"Ubuntu","name":"24.04 (LTS) x64","status":"available"},{"slug":null,"distribution":"Custom","name":"snap"}],"links":{}}""");

        var result = await _provider.GetCatalogAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["fra1"], result.Data!.Regions.Select(r => r.Id));
        Assert.Equal(["s-1vcpu-1gb", "s-2vcpu-2gb"], result.Data.Sizes.Select(s => s.Id));
        Assert.Contains("1 GB RAM", result.Data.Sizes[0].Name);
        Assert.Equal(["ubuntu-24-04-x64"], result.Data.Images.Select(i => i.Id));
    }

    [Fact]
    public async Task CreateServerAsync_PostsDroplet()
    {
        _handler.Add("/v2/droplets", """{"droplet":{"id":77,"name":"new-1","status":"new","networks":{"v4":[]},"region":{"slug":"fra1"},"size_slug":"s-1vcpu-1gb"}}""", HttpStatusCode.Accepted);

        var result = await _provider.CreateServerAsync(Token, new CloudCreateServerRequest("new-1", "fra1", "s-1vcpu-1gb", "ubuntu-24-04-x64", "#cloud-config\n"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("77", result.Data!.Server.ExternalId);
        Assert.Null(result.Data.Server.PublicIpv4);
        Assert.Null(result.Data.RootPassword);
        var request = Assert.Single(_handler.Requests);
        Assert.Contains("\"region\":\"fra1\"", request.Body);
        Assert.Contains("\"user_data\"", request.Body);
    }

    [Fact]
    public async Task CreateServerAsync_OmitsEmptyUserData()
    {
        _handler.Add("/v2/droplets", """{"droplet":{"id":78,"name":"n","status":"new"}}""", HttpStatusCode.Accepted);

        await _provider.CreateServerAsync(Token, new CloudCreateServerRequest("n", "fra1", "s-1vcpu-1gb", "ubuntu-24-04-x64", null), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("user_data", _handler.Requests.Single().Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "API anahtarı geçersiz veya iptal edilmiş.")]
    [InlineData(HttpStatusCode.TooManyRequests, "DigitalOcean istek sınırına ulaşıldı; biraz sonra tekrar deneyin.")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "DigitalOcean: Droplet name is invalid")]
    public void ErrorMessage_MapsStatus(HttpStatusCode status, string expected)
    {
        Assert.Equal(expected, DigitalOceanCloudProvider.ErrorMessage(status, """{"id":"unprocessable_entity","message":"Droplet name is invalid"}"""));
    }
}
