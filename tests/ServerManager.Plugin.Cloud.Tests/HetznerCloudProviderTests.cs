using System.Net;
using Microsoft.Extensions.Options;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Plugin.Cloud.Hetzner;
using ServerManager.Plugin.Cloud.Tests.Fakes;

namespace ServerManager.Plugin.Cloud.Tests;

public class HetznerCloudProviderTests
{
    private const string Token = "test-token-123";

    private readonly ScriptedHttpHandler _handler = new();
    private readonly HetznerCloudProvider _provider;

    public HetznerCloudProviderTests()
    {
        _provider = new HetznerCloudProvider(new StubHttpClientFactory(_handler), Options.Create(new HetznerOptions { ApiUrl = "https://hetzner.test/v1/" }));
    }

    private static string Server(int id, string name, string ip, string location) => $$$"""
        {"id":{{{id}}},"name":"{{{name}}}","status":"running","created":"2026-09-01T10:00:00+00:00",
         "public_net":{"ipv4":{"ip":"{{{ip}}}"}},
         "datacenter":{"location":{"name":"{{{location}}}"}},
         "server_type":{"name":"cx22","prices":[
            {"location":"fsn1","price_monthly":{"net":"3.7900000000","gross":"4.5101000000"}},
            {"location":"hel1","price_monthly":{"net":"3.2900000000","gross":"3.9151000000"}}]}}
        """;

    [Fact]
    public async Task ListServersAsync_FollowsPaginationAndUsesLocationPrice()
    {
        _handler
            .Add("/v1/servers?page=1", """{"servers":[""" + Server(1, "web-1", "198.51.100.1", "fsn1") + """],"meta":{"pagination":{"next_page":2}}}""")
            .Add("/v1/servers?page=2", """{"servers":[""" + Server(2, "web-2", "198.51.100.2", "hel1") + """],"meta":{"pagination":{"next_page":null}}}""");

        var result = await _provider.ListServersAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        var first = result.Data[0];
        Assert.Equal("1", first.ExternalId);
        Assert.Equal("198.51.100.1", first.PublicIpv4);
        Assert.Equal("fsn1", first.Region);
        Assert.Equal("cx22", first.Size);
        Assert.Equal(3.79m, first.MonthlyPrice);
        Assert.Equal("EUR", first.Currency);
        Assert.Equal(3.29m, result.Data[1].MonthlyPrice);
        Assert.All(_handler.Requests, r => Assert.Equal($"Bearer {Token}", r.Authorization));
    }

    [Fact]
    public async Task ValidateTokenAsync_MapsUnauthorizedWithoutLeakingToken()
    {
        _handler.Add("/v1/servers", """{"error":{"code":"unauthorized","message":"unable to authenticate"}}""", HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateTokenAsync(Token, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("API anahtarı geçersiz veya iptal edilmiş.", result.Message);
        Assert.DoesNotContain(Token, result.Message);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsProjectLabel()
    {
        _handler.Add("/v1/servers", """{"servers":[],"meta":{"pagination":{"total_entries":4,"next_page":null}}}""");

        var result = await _provider.ValidateTokenAsync(Token, TestContext.Current.CancellationToken);

        Assert.Equal("Proje API anahtarı · 4 sunucu", result.Data);
    }

    [Fact]
    public async Task GetCatalogAsync_SkipsDeprecatedAndSortsByPrice()
    {
        _handler
            .Add("/v1/locations", """{"locations":[{"name":"fsn1","city":"Falkenstein","description":"DC Park 1"}]}""")
            .Add("/v1/server_types", """
                {"server_types":[
                  {"name":"cx32","cores":4,"memory":8,"disk":80,"deprecated":false,"deprecation":null,"architecture":"x86","prices":[{"location":"fsn1","price_monthly":{"net":"6.8000000000"}}]},
                  {"name":"cx22","cores":2,"memory":4,"disk":40,"deprecated":false,"deprecation":null,"architecture":"x86","prices":[{"location":"fsn1","price_monthly":{"net":"3.7900000000"}}]},
                  {"name":"cx11","cores":1,"memory":2,"disk":20,"deprecated":true,"prices":[{"location":"fsn1","price_monthly":{"net":"3.2900000000"}}]}]}
                """)
            .Add("/v1/images", """
                {"images":[
                  {"name":"ubuntu-24.04","description":"Ubuntu 24.04","architecture":"x86","deprecated":null},
                  {"name":"ubuntu-24.04","description":"Ubuntu 24.04","architecture":"arm","deprecated":null},
                  {"name":"centos-7","description":"CentOS 7","architecture":"x86","deprecated":"2024-06-30T00:00:00+00:00"}]}
                """);

        var result = await _provider.GetCatalogAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var catalog = result.Data!;
        Assert.Equal(["fsn1"], catalog.Regions.Select(r => r.Id));
        Assert.Equal(["cx22", "cx32"], catalog.Sizes.Select(s => s.Id));
        Assert.Equal(["fsn1"], catalog.Sizes[0].Regions!);
        var image = Assert.Single(catalog.Images);
        Assert.Equal("ubuntu-24.04", image.Id);
        Assert.Equal("x86, arm", image.Description);
    }

    [Fact]
    public async Task CreateServerAsync_SendsUserDataAndReturnsRootPassword()
    {
        _handler.Add("/v1/servers", """{"server":""" + Server(9, "new-1", "198.51.100.9", "fsn1") + ""","root_password":"Gecici-Parola-1"}""", HttpStatusCode.Created);

        var result = await _provider.CreateServerAsync(Token, new CloudCreateServerRequest("new-1", "fsn1", "cx22", "ubuntu-24.04", "#cloud-config\n"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("9", result.Data!.Server.ExternalId);
        Assert.Equal("Gecici-Parola-1", result.Data.RootPassword);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("\"server_type\":\"cx22\"", request.Body);
        Assert.Contains("\"location\":\"fsn1\"", request.Body);
        Assert.Contains("\"user_data\":\"#cloud-config\\n\"", request.Body);
    }

    [Fact]
    public async Task CreateServerAsync_ReportsForbiddenForReadOnlyToken()
    {
        _handler.Add("/v1/servers", """{"error":{"code":"forbidden","message":"insufficient permissions"}}""", HttpStatusCode.Forbidden);

        var result = await _provider.CreateServerAsync(Token, new CloudCreateServerRequest("n", "fsn1", "cx22", "ubuntu-24.04", null), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("Read & Write", result.Message);
    }

    [Fact]
    public async Task ListServersAsync_ReportsNetworkFailure()
    {
        _handler.Throw = new HttpRequestException("boom");

        var result = await _provider.ListServersAsync(Token, TestContext.Current.CancellationToken);

        Assert.Equal("Hetzner API'ye bağlanılamadı.", result.Message);
    }

    [Fact]
    public void ErrorMessage_UsesProviderDetailForOtherErrors()
    {
        var message = HetznerCloudProvider.ErrorMessage(HttpStatusCode.UnprocessableEntity, """{"error":{"code":"uniqueness_error","message":"server name is already used"}}""");

        Assert.Equal("Hetzner: server name is already used", message);
    }
}
