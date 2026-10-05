using System.Net;
using ServerManager.Application.Authorization;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class PermissionTests(ServerManagerWebFactory factory)
{
    [Theory]
    [InlineData("/Users")]
    [InlineData("/AuditLogs")]
    [InlineData("/Settings")]
    public async Task Viewer_without_permission_is_redirected_to_access_denied(string path)
    {
        using var client = factory.CreateTestClient();
        var email = await factory.CreateUserAsync(Roles.Viewer);
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Viewer_without_permission_gets_403_for_ajax_request()
    {
        using var client = factory.CreateTestClient();
        var email = await factory.CreateUserAsync(Roles.Viewer);
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, TestContext.Current.CancellationToken);
        using var request = HttpClientAuthExtensions.JsonGet("/Users");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_can_open_pages_it_has_permission_for()
    {
        using var client = factory.CreateTestClient();
        var email = await factory.CreateUserAsync(Roles.Viewer);
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync("/Servers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_can_open_user_management()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync("/Users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
