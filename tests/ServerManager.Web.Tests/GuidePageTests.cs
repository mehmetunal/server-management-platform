using System.Net;
using ServerManager.Application.Authorization;
using ServerManager.Web.Helpers;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class GuidePageTests(ServerManagerWebFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Guide_requires_authentication()
    {
        using var client = factory.CreateTestClient();

        using var response = await client.GetAsync("/Guide", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Viewer_can_read_the_guide()
    {
        using var client = factory.CreateTestClient();
        var email = await factory.CreateUserAsync(Roles.Viewer);
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, Ct);

        using var response = await client.GetAsync("/Guide", Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-guide-toc", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"{GuideAnchors.Servers}\"", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"{GuideAnchors.Troubleshooting}\"", html, StringComparison.Ordinal);
        Assert.Contains("Kullanım Kılavuzu", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Module_pages_link_to_the_guide()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);

        using var response = await client.GetAsync("/Servers", Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"href=\"/Guide#{GuideAnchors.Servers}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_guide_image_returns_not_found()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);

        using var response = await client.GetAsync("/Guide/Image/..%2Fappsettings.json", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
