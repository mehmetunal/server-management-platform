using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Plugin.Cloud.Tests.Fakes;
using ServerManager.Plugin.Cloud.Vultr;

namespace ServerManager.Plugin.Cloud.Tests;

public class VultrCloudProviderTests
{
    private const string Token = "vultr-test-token";

    private readonly ScriptedHttpHandler _handler = new();
    private readonly VultrCloudProvider _provider;

    public VultrCloudProviderTests()
    {
        _provider = new VultrCloudProvider(new StubHttpClientFactory(_handler), Options.Create(new VultrOptions { ApiUrl = "https://vultr.test/v2/" }));
    }

    [Fact]
    public async Task ListServersAsync_ReadsIpAndFollowsCursor()
    {
        _handler
            .Add("/v2/instances?per_page=100", """{"instances":[{"id":"i-1","label":"app-1","status":"active","main_ip":"203.0.113.1","region":"ams","plan":"vc2-1c-1gb","date_created":"2026-09-01T10:00:00+00:00"}],"meta":{"links":{"next":"page-2"}}}""")
            .Add("/v2/instances?per_page=100&cursor=page-2", """{"instances":[{"id":"i-2","label":"app-2","status":"pending","main_ip":"0.0.0.0","region":"ewr","plan":"vc2-1c-2gb"}],"meta":{"links":{"next":""}}}""")
            .Add("/v2/plans?per_page=100", """{"plans":[{"id":"vc2-1c-1gb","monthly_cost":5},{"id":"vc2-1c-2gb","monthly_cost":10}],"meta":{"links":{"next":""}}}""");

        var result = await _provider.ListServersAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["app-1", "app-2"], result.Data!.Select(s => s.Name));
        Assert.Equal("203.0.113.1", result.Data![0].PublicIpv4);
        Assert.Equal(5.00m, result.Data[0].MonthlyPrice);
        Assert.Equal("USD", result.Data[0].Currency);
        Assert.Null(result.Data[1].PublicIpv4);
        Assert.Equal($"Bearer {Token}", _handler.Requests[0].Authorization);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsAccountName()
    {
        _handler.Add("/v2/account", """{"account":{"name":"Ops","balance":12.5}}""");

        var result = await _provider.ValidateTokenAsync(Token, TestContext.Current.CancellationToken);

        Assert.Equal("Vultr · Ops", result.Data);
    }

    [Fact]
    public async Task GetCatalogAsync_KeepsPlansWithLocations()
    {
        _handler
            .Add("/v2/regions?per_page=100", """{"regions":[{"id":"ams","city":"Amsterdam","country":"NL"}],"meta":{"links":{}}}""")
            .Add("/v2/plans?per_page=100", """{"plans":[{"id":"vc2-1c-2gb","vcpu_count":1,"ram":2048,"disk":55,"monthly_cost":10,"type":"vc2","locations":["ams"]},{"id":"bare","locations":[]}],"meta":{"links":{}}}""")
            .Add("/v2/os?per_page=100", """{"os":[{"id":2284,"name":"Ubuntu 24.04 LTS x64","arch":"x64"}],"meta":{"links":{}}}""");

        var result = await _provider.GetCatalogAsync(Token, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["ams"], result.Data!.Regions.Select(r => r.Id));
        Assert.Equal(["vc2-1c-2gb"], result.Data.Sizes.Select(s => s.Id));
        Assert.Contains("2 GB RAM", result.Data.Sizes[0].Name);
        Assert.Equal(["2284"], result.Data.Images.Select(i => i.Id));
    }

    [Fact]
    public async Task CreateServerAsync_Base64EncodesUserData()
    {
        _handler.Add("/v2/instances", """{"instance":{"id":"i-9","label":"new-1","status":"pending","main_ip":"0.0.0.0","region":"ams","plan":"vc2-1c-1gb","default_password":"Once-1"}}""", HttpStatusCode.Accepted);

        var result = await _provider.CreateServerAsync(Token, new CloudCreateServerRequest("new-1", "ams", "vc2-1c-1gb", "2284", "#cloud-config\n"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Once-1", result.Data!.RootPassword);
        Assert.Null(result.Data.Server.PublicIpv4);
        var body = _handler.Requests.Single().Body;
        Assert.Contains("\"os_id\":2284", body);
        Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("#cloud-config\n")), body);
    }

    [Fact]
    public async Task CreateServerAsync_OmitsEmptyUserData()
    {
        _handler.Add("/v2/instances", """{"instance":{"id":"i-8","label":"n","status":"pending"}}""", HttpStatusCode.Accepted);

        await _provider.CreateServerAsync(Token, new CloudCreateServerRequest("n", "ams", "vc2-1c-1gb", "2284", null), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("user_data", _handler.Requests.Single().Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "API anahtarı geçersiz veya iptal edilmiş.")]
    [InlineData(HttpStatusCode.TooManyRequests, "Vultr istek sınırına ulaşıldı; biraz sonra tekrar deneyin.")]
    [InlineData(HttpStatusCode.BadRequest, "Vultr: plan is not available in region")]
    public void ErrorMessage_MapsStatus(HttpStatusCode status, string expected)
    {
        Assert.Equal(expected, VultrCloudProvider.ErrorMessage(status, """{"error":"plan is not available in region"}"""));
    }
}
