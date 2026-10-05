using System.Net;
using ServerManager.Application.Authorization;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class AuthenticationTests(ServerManagerWebFactory factory)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Servers")]
    [InlineData("/Users")]
    [InlineData("/Settings")]
    public async Task Anonymous_page_request_redirects_to_login(string path)
    {
        using var client = factory.CreateTestClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Anonymous_ajax_request_gets_401_instead_of_redirect()
    {
        using var client = factory.CreateTestClient();
        using var request = HttpClientAuthExtensions.JsonGet("/Servers");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_post_to_protected_controller_is_denied()
    {
        using var client = factory.CreateTestClient();
        var token = await client.GetAntiforgeryTokenAsync("/Account/Login", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Users/Create")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = "x@example.com" })
        };
        request.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_page_is_reachable_anonymously()
    {
        using var client = factory.CreateTestClient();

        using var response = await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Valid_login_issues_auth_cookie_and_opens_dashboard()
    {
        using var client = factory.CreateTestClient();

        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, TestContext.Current.CancellationToken);
        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        using var client = factory.CreateTestClient();
        var email = await factory.CreateUserAsync(Roles.Viewer);

        using var response = await client.PostLoginAsync(email, "Wrong-Password-123", TestContext.Current.CancellationToken);
        using var dashboard = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
    }
}
