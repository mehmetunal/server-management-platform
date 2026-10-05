using System.Net;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class AntiforgeryTests(ServerManagerWebFactory factory)
{
    [Fact]
    public async Task Login_post_without_antiforgery_token_is_rejected()
    {
        using var client = factory.CreateTestClient();
        await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);

        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = ServerManagerWebFactory.AdminEmail,
            ["Password"] = ServerManagerWebFactory.AdminPassword
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_state_changing_post_requires_antiforgery_token()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, TestContext.Current.CancellationToken);

        using var withoutToken = new HttpRequestMessage(HttpMethod.Post, "/Account/Logout");
        withoutToken.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var rejected = await client.SendAsync(withoutToken, TestContext.Current.CancellationToken);

        using var forged = new HttpRequestMessage(HttpMethod.Post, "/Account/Logout");
        forged.Headers.Add("X-Requested-With", "XMLHttpRequest");
        forged.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, "CfDJ8-not-a-real-token");
        using var forgedResponse = await client.SendAsync(forged, TestContext.Current.CancellationToken);

        // Kimliğe bağlı token girişten sonra yeniden alınır.
        var token = await client.GetAntiforgeryTokenAsync("/Account/Security", TestContext.Current.CancellationToken);
        using var valid = new HttpRequestMessage(HttpMethod.Post, "/Account/Logout");
        valid.Headers.Add("X-Requested-With", "XMLHttpRequest");
        valid.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, token);
        using var accepted = await client.SendAsync(valid, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, forgedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Token_from_anonymous_session_is_not_valid_after_login()
    {
        using var client = factory.CreateTestClient();
        var anonymousToken = await client.GetAntiforgeryTokenAsync("/Account/Login", TestContext.Current.CancellationToken);
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Logout");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, anonymousToken);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
