using System.Net;
using ServerManager.Application.Authorization;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class ProjectEnvironmentPageTests(ServerManagerWebFactory factory)
{
    private const string Environment = "# db\nDATABASE_URL=postgres://db\nAPI_KEY=gizli-deger\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Project_page_renders_tabs_with_masked_values()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var projectId = await ProjectWebhookApiTests.SeedProjectAsync(factory, autoDeploy: false, Environment);

        using var response = await client.GetAsync($"/Projects/Details/{projectId}", Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-tab=\"environment\"", html, StringComparison.Ordinal);
        Assert.Contains("data-tab=\"webhook\"", html, StringComparison.Ordinal);
        Assert.Contains("API_KEY", html, StringComparison.Ordinal);
        Assert.DoesNotContain("gizli-deger", html, StringComparison.Ordinal);
        Assert.Contains($"/api/webhooks/projects/{projectId}", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_can_export_environment_file()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var projectId = await ProjectWebhookApiTests.SeedProjectAsync(factory, autoDeploy: false, Environment);

        using var response = await client.GetAsync($"/ProjectEnvironment/Export/{projectId}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Environment, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Viewer_cannot_export_or_reveal_values()
    {
        using var client = factory.CreateTestClient();
        var email = await factory.CreateUserAsync(Roles.Viewer);
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, Ct);
        var projectId = await ProjectWebhookApiTests.SeedProjectAsync(factory, autoDeploy: false, Environment);
        using var request = HttpClientAuthExtensions.JsonGet($"/ProjectEnvironment/Export/{projectId}");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
