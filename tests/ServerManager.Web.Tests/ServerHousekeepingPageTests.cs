using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Authorization;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

/// <summary>Temizlik ve Kaynak Kullanımı sayfalarının yetki kontrolleri (sayfalar SSH'a gitmeden açılır; veriler AJAX ile gelir).</summary>
[Collection(WebCollection.Name)]
public sealed class ServerHousekeepingPageTests(ServerManagerWebFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> SeedServerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = new Server { Name = "hk-" + Guid.NewGuid().ToString("N")[..8], Hostname = "hk", IpAddress = "203.0.113.30", Username = "deploy" };
        db.Servers.Add(server);
        await db.SaveChangesAsync(Ct);
        return server.Id;
    }

    private async Task<HttpClient> LoginAsync(string? role = null, params string[] permissions)
    {
        var client = factory.CreateTestClient();
        if (role is null && permissions.Length == 0)
        {
            await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
            return client;
        }

        var email = role is not null ? await factory.CreateUserAsync(role) : await factory.CreateUserWithPermissionsAsync(permissions);
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, Ct);
        return client;
    }

    private static void AssertAccessDenied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_sees_both_pages_tabs_and_sidebar_links()
    {
        var serverId = await SeedServerAsync();
        using var client = await LoginAsync();

        using var cleanup = await client.GetAsync($"/ServerCleanup/Index/{serverId}", Ct);
        using var resources = await client.GetAsync($"/ServerResources/Index/{serverId}", Ct);
        var cleanupHtml = await cleanup.Content.ReadAsStringAsync(Ct);
        var resourcesHtml = await resources.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, cleanup.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resources.StatusCode);
        Assert.Contains($"/ServerCleanup/Scan/{serverId}", cleanupHtml, StringComparison.Ordinal);
        Assert.Contains("/ServerPicker/Cleanup", cleanupHtml, StringComparison.Ordinal);
        Assert.Contains($"/ServerResources/Overview/{serverId}", resourcesHtml, StringComparison.Ordinal);
        Assert.Contains($"/ServerCleanup/Index/{serverId}", resourcesHtml, StringComparison.Ordinal);
        Assert.Contains("/ServerPicker/Resources", resourcesHtml, StringComparison.Ordinal);
        Assert.Contains("data-auto-refresh-toggle", resourcesHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/ServerPicker/Cleanup")]
    [InlineData("/ServerPicker/Resources")]
    public async Task Admin_can_open_server_pickers(string path)
    {
        using var client = await LoginAsync();

        using var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operator_can_open_cleanup()
    {
        var serverId = await SeedServerAsync();
        using var client = await LoginAsync(Roles.Operator);

        using var response = await client.GetAsync($"/ServerCleanup/Index/{serverId}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Developer_sees_resources_but_not_cleanup()
    {
        var serverId = await SeedServerAsync();
        using var client = await LoginAsync(Roles.Developer);

        using var resources = await client.GetAsync($"/ServerResources/Index/{serverId}", Ct);
        using var cleanup = await client.GetAsync($"/ServerCleanup/Index/{serverId}", Ct);
        using var picker = await client.GetAsync("/ServerPicker/Cleanup", Ct);
        var html = await resources.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, resources.StatusCode);
        Assert.DoesNotContain($"/ServerCleanup/Index/{serverId}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/ServerPicker/Cleanup", html, StringComparison.Ordinal);
        AssertAccessDenied(cleanup);
        AssertAccessDenied(picker);
    }

    [Theory]
    [InlineData("/ServerCleanup/Index/{0}")]
    [InlineData("/ServerCleanup/Scan/{0}")]
    [InlineData("/ServerResources/Index/{0}")]
    [InlineData("/ServerResources/Overview/{0}")]
    [InlineData("/ServerResources/Disk/{0}")]
    public async Task Viewer_without_permissions_is_denied(string pathFormat)
    {
        var serverId = await SeedServerAsync();
        using var client = await LoginAsync(Roles.Viewer);

        using var response = await client.GetAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, pathFormat, serverId), Ct);

        AssertAccessDenied(response);
    }

    [Fact]
    public async Task Cleanup_permission_alone_is_enough_for_cleanup_page()
    {
        var serverId = await SeedServerAsync();
        using var client = await LoginAsync(null, Permissions.ServerView, Permissions.ServerCleanup);

        using var cleanup = await client.GetAsync($"/ServerCleanup/Index/{serverId}", Ct);
        using var resources = await client.GetAsync($"/ServerResources/Index/{serverId}", Ct);

        Assert.Equal(HttpStatusCode.OK, cleanup.StatusCode);
        AssertAccessDenied(resources);
    }

    [Fact]
    public async Task Execute_requires_permission_antiforgery_and_a_selection()
    {
        var serverId = await SeedServerAsync();
        var path = $"/ServerCleanup/Execute/{serverId}";

        using var developer = await LoginAsync(Roles.Developer);
        var developerToken = await developer.GetAntiforgeryTokenAsync($"/ServerResources/Index/{serverId}", Ct);
        using var forbidden = await developer.SendAsync(AjaxPost(path, developerToken, new Dictionary<string, string> { ["keys"] = "journal" }), Ct);

        using var admin = await LoginAsync();
        using var withoutToken = await admin.SendAsync(AjaxPost(path, null, new Dictionary<string, string> { ["keys"] = "journal" }), Ct);
        var token = await admin.GetAntiforgeryTokenAsync($"/ServerCleanup/Index/{serverId}", Ct);
        using var empty = await admin.SendAsync(AjaxPost(path, token, new Dictionary<string, string> { ["dryRun"] = "true" }), Ct);
        var body = await empty.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("Temizlenecek öğe seçilmedi", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_server_returns_not_found()
    {
        using var client = await LoginAsync();

        using var cleanup = await client.GetAsync($"/ServerCleanup/Index/{Guid.NewGuid()}", Ct);
        using var resources = await client.GetAsync($"/ServerResources/Index/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, cleanup.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resources.StatusCode);
    }

    private static HttpRequestMessage AjaxPost(string path, string? token, IDictionary<string, string> form)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("Accept", "application/json");
        if (token is not null)
            request.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, token);
        return request;
    }
}
