using System.Net;
using System.Net.Http.Headers;
using ServerManager.Application.Agent;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class AgentApiTests(ServerManagerWebFactory factory)
{
    private const string ReportPath = "/api/agent/report";

    [Fact]
    public async Task Report_without_authorization_header_is_rejected()
    {
        using var client = factory.CreateTestClient();

        using var response = await client.PostAsync(ReportPath, new StringContent("cpu 1 2 3"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Bearer")]
    [InlineData("Bearer not-a-token")]
    [InlineData("Basic c21hX3Rlc3Q=")]
    public async Task Report_with_malformed_token_is_rejected(string authorization)
    {
        using var client = factory.CreateTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, ReportPath) { Content = new StringContent("cpu 1 2 3") };
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Report_with_well_formed_but_unknown_token_is_rejected()
    {
        using var client = factory.CreateTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, ReportPath) { Content = new StringContent("cpu 1 2 3") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AgentTokens.Generate());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Report_endpoint_does_not_require_cookie_or_antiforgery()
    {
        using var client = factory.CreateTestClient();

        using var response = await client.PostAsync(ReportPath, new StringContent(string.Empty), TestContext.Current.CancellationToken);

        // Kimlik doğrulaması Bearer token ile yapılır: giriş sayfasına yönlendirme veya antiforgery 400'ü olmamalı.
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
