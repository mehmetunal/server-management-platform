using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Plugin.Cloud.Linode;
using ServerManager.Plugin.Cloud.Tests.Fakes;

namespace ServerManager.Plugin.Cloud.Tests;

public class LinodeCloudProviderTests
{
    private const string Token = "linode-test-token";

    private readonly ScriptedHttpHandler _handler = new();
    private readonly LinodeCloudProvider _provider;

    public LinodeCloudProviderTests()
    {
        _provider = new LinodeCloudProvider(new StubHttpClientFactory(_handler), Options.Create(new LinodeOptions { ApiUrl = "https://linode.test/v4/" }));
    }

    [Fact]
    public async Task ListServersAsync_SkipsPrivateIpAndReadsPrice()
    {
        _handler
            .Add("/v4/linode/instances?page=1", """{"data":[{"id":7,"label":"app-1","status":"running","type":"g6-nanode-1","region":"eu-central","ipv4":["192.168.1.4","203.0.113.9"],"created":"2026-09-01T10:00:00"}],"page":1,"pages":1,"results":1}""")
            .Add("/v4/linode/types?page=1", """{"data":[{"id":"g6-nanode-1","price":{"monthly":5}}],"page":1,"pages":1}""");

        var result = await _provider.ListServersAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var server = Assert.Single(result.Data!);
        Assert.Equal("203.0.113.9", server.PublicIpv4);
        Assert.Equal(5.00m, server.MonthlyPrice);
        Assert.Equal("USD", server.Currency);
        Assert.Equal("7", server.ExternalId);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsServerCount()
    {
        _handler.Add("/v4/linode/instances?page=1&page_size=1", """{"data":[],"page":1,"pages":1,"results":3}""");

        var result = await _provider.ValidateTokenAsync(Token, TestContext.Current.CancellationToken);

        Assert.Equal("Linode hesabı · 3 sunucu", result.Data);
        Assert.Equal($"Bearer {Token}", _handler.Requests.Single().Authorization);
    }

    [Fact]
    public async Task GetCatalogAsync_KeepsPublicImagesAndReadyRegions()
    {
        _handler
            .Add("/v4/regions?page=1", """{"data":[{"id":"eu-central","label":"Frankfurt, DE","status":"ok","capabilities":["Linodes","Metadata"]},{"id":"old","label":"Old","status":"outage","capabilities":["Linodes"]}],"page":1,"pages":1}""")
            .Add("/v4/linode/types?page=1", """{"data":[{"id":"g6-nanode-1","label":"Nanode 1GB","vcpus":1,"memory":1024,"disk":25600,"price":{"monthly":5}}],"page":1,"pages":1}""")
            .Add("/v4/images?page=1", """{"data":[{"id":"linode/ubuntu24.04","label":"Ubuntu 24.04 LTS","status":"available"},{"id":"private/mine","label":"Mine","status":"available"}],"page":1,"pages":1}""");

        var result = await _provider.GetCatalogAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["eu-central"], result.Data!.Regions.Select(r => r.Id));
        Assert.Equal(["g6-nanode-1"], result.Data.Sizes.Select(s => s.Id));
        Assert.Contains("1 GB RAM", result.Data.Sizes[0].Name);
        Assert.Equal(["linode/ubuntu24.04"], result.Data.Images.Select(i => i.Id));
        Assert.Null(result.Data.Sizes[0].Regions);
    }

    [Fact]
    public async Task CreateServerAsync_SendsPasswordAndUserData()
    {
        _handler.Add("/v4/linode/instances", """{"id":9,"label":"web-1","status":"provisioning","region":"eu-central","type":"g6-nanode-1","ipv4":[]}""");

        var result = await _provider.CreateServerAsync(Token, new CloudCreateServerRequest("web-1", "eu-central", "g6-nanode-1", "linode/ubuntu24.04", "#cloud-config\n"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.RootPassword));
        var body = _handler.Requests.Single().Body;
        Assert.Contains("\"root_pass\"", body);
        Assert.Contains(result.Data.RootPassword!, body);
        Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("#cloud-config\n")), body);
    }

    [Fact]
    public async Task CreateServerAsync_RejectsLabelBeforeCallingApi()
    {
        var result = await _provider.CreateServerAsync(Token, new CloudCreateServerRequest("1bad", "eu-central", "g6-nanode-1", "linode/ubuntu24.04", null), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("harfle", result.Message);
        Assert.Empty(_handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "API anahtarı geçersiz veya iptal edilmiş.")]
    [InlineData(HttpStatusCode.TooManyRequests, "Linode istek sınırına ulaşıldı; biraz sonra tekrar deneyin.")]
    [InlineData(HttpStatusCode.BadRequest, "Linode: Invalid region")]
    public void ErrorMessage_MapsStatus(HttpStatusCode status, string expected)
    {
        Assert.Equal(expected, LinodeCloudProvider.ErrorMessage(status, """{"errors":[{"reason":"Invalid region"}]}"""));
    }
}
