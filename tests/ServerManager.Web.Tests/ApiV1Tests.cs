using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Authorization;
using ServerManager.Infrastructure.Identity;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Web.Tests.Infrastructure;
using static ServerManager.Web.Tests.Infrastructure.ApiKeyTestExtensions;

namespace ServerManager.Web.Tests;

/// <summary>REST API v1: API anahtarı kimlik doğrulaması, kapsam, cookie reddi ve yanıt biçimleri.</summary>
[Collection(WebCollection.Name)]
public sealed class ApiV1Tests(ServerManagerWebFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Missing_authorization_header_returns_401_problem()
    {
        using var client = factory.CreateTestClient();

        using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/servers", null), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-key")]
    [InlineData("smk_abcdefghijkl_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopq")]
    public async Task Wrong_key_returns_401(string token)
    {
        using var client = factory.CreateTestClient();

        using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", token), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tampered_secret_of_existing_key_returns_401()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        var token = await factory.CreateApiKeyAsync(email, [Permissions.ServerView]);
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');
        using var client = factory.CreateTestClient();

        using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", tampered), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_key_authenticates_and_reports_effective_permissions()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView, Permissions.AlertView);
        var token = await factory.CreateApiKeyAsync(email, [Permissions.ServerView]);
        using var client = factory.CreateTestClient();

        using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", token), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(email, json.RootElement.GetProperty("userName").GetString());
        Assert.Equal([Permissions.ServerView], json.RootElement.GetProperty("permissions").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Expired_key_returns_401()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        var token = await factory.CreateApiKeyAsync(email, [Permissions.ServerView]);
        await UpdateKeyAsync(token, db => db.ApiKeys.Where(k => k.Prefix == Prefix(token))
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)), Ct));
        using var client = factory.CreateTestClient();

        using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", token), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("süresi dolmuş", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoked_key_returns_401()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        var token = await factory.CreateApiKeyAsync(email, [Permissions.ServerView]);
        using var client = factory.CreateTestClient();
        using (var before = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", token), Ct))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await UpdateKeyAsync(token, db => db.ApiKeys.Where(k => k.Prefix == Prefix(token))
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.RevokedAt, DateTime.UtcNow), Ct));

        using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", token), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Key_used_from_ip_outside_allow_list_returns_401()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        // Test istemcilerinin IP'si 10.77.x.x'tir.
        var blocked = await factory.CreateApiKeyAsync(email, [Permissions.ServerView], allowedIps: "192.0.2.0/24");
        var allowed = await factory.CreateApiKeyAsync(email, [Permissions.ServerView], allowedIps: "10.77.0.0/16");
        using var client = factory.CreateTestClient();

        using var rejected = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", blocked), Ct);
        using var accepted = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", allowed), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Key_of_inactive_user_returns_401()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        var token = await factory.CreateApiKeyAsync(email, [Permissions.ServerView]);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            user!.IsActive = false;
            await users.UpdateAsync(user);
        }

        using var client = factory.CreateTestClient();
        using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", token), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_outside_key_scope_returns_403()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView, Permissions.AlertView);
        var token = await factory.CreateApiKeyAsync(email, [Permissions.AlertView]);
        using var client = factory.CreateTestClient();

        using var servers = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/servers", token), Ct);
        using var alerts = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/alerts", token), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, servers.StatusCode);
        Assert.Equal("application/problem+json", servers.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, alerts.StatusCode);
    }

    [Fact]
    public async Task Permission_removed_from_role_is_denied_immediately_even_if_in_scope()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        var token = await factory.CreateApiKeyAsync(email, [Permissions.ServerView]);
        using var client = factory.CreateTestClient();
        using (var before = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/servers", token), Ct))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var roleName = (await users.GetRolesAsync((await users.FindByEmailAsync(email))!)).Single();
            await roles.RemoveClaimAsync((await roles.FindByNameAsync(roleName))!, new Claim(Permissions.ClaimType, Permissions.ServerView));
        }

        using var after = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/servers", token), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    }

    [Fact]
    public async Task Cookie_session_cannot_call_api()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        using (var page = await client.GetAsync("/Servers", Ct))
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        using var get = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/servers", null), Ct);
        var token = await client.GetAntiforgeryTokenAsync("/Servers", Ct);
        using var post = ApiRequest(HttpMethod.Post, $"/api/v1/alerts/{Guid.NewGuid()}/acknowledge", null);
        post.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, token);
        using var postResponse = await client.SendAsync(post, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
    }

    [Fact]
    public async Task Api_key_post_does_not_need_antiforgery_token()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.AlertView, Permissions.AlertAcknowledge);
        var token = await factory.CreateApiKeyAsync(email, [Permissions.AlertView, Permissions.AlertAcknowledge]);
        using var client = factory.CreateTestClient();

        using var response = await client.SendAsync(ApiRequest(HttpMethod.Post, $"/api/v1/alerts/{Guid.NewGuid()}/acknowledge", token), Ct);

        // Antiforgery reddi 400 olurdu; kimliği doğrulanmış istek servise ulaşır ve alarm bulunamaz.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_endpoints_return_expected_shapes()
    {
        var email = await factory.CreateUserWithPermissionsAsync(
            Permissions.ServerView, Permissions.DeploymentView, Permissions.ServicesView, Permissions.BackupView, Permissions.AlertView);
        var token = await factory.CreateApiKeyAsync(email,
            [Permissions.ServerView, Permissions.DeploymentView, Permissions.ServicesView, Permissions.BackupView, Permissions.AlertView]);
        using var client = factory.CreateTestClient();

        foreach (var path in new[] { "/api/v1/servers?pageSize=5", "/api/v1/projects", "/api/v1/backups/runs", "/api/v1/alerts?status=all" })
        {
            using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, path, token), Ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode}");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("items").ValueKind);
            Assert.True(json.RootElement.TryGetProperty("totalCount", out _), path);
            Assert.True(json.RootElement.TryGetProperty("totalPages", out _), path);
            Assert.True(json.RootElement.GetProperty("page").GetInt32() >= 1, path);
        }

        foreach (var path in new[] { "/api/v1/services", "/api/v1/backups/jobs" })
        {
            using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, path, token), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        }

        using var missing = await client.SendAsync(ApiRequest(HttpMethod.Get, $"/api/v1/servers/{Guid.NewGuid()}", token), Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);

        using var invalid = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/servers?status=bogus", token), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_requires_authentication_and_lists_endpoints()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        var token = await factory.CreateApiKeyAsync(email, [Permissions.ServerView]);
        using var client = factory.CreateTestClient();

        using var anonymous = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/openapi.json", null), Ct);
        using var response = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/openapi.json", token), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var paths = json.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("/api/v1/servers", paths);
        Assert.Contains("/api/v1/projects/{id}/deployments", paths);
        Assert.Contains("/api/v1/backups/runs/{id}/download", paths);
        Assert.DoesNotContain(paths, p => !p.StartsWith("/api/v1/", StringComparison.Ordinal));

        // Belge panelden de açılabilir (salt okunur GET; oturum çereziyle).
        using var browser = factory.CreateTestClient();
        await browser.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        using var fromPanel = await browser.GetAsync("/api/v1/openapi.json", Ct);
        Assert.Equal(HttpStatusCode.OK, fromPanel.StatusCode);
    }

    [Fact]
    public async Task Api_docs_and_key_pages_open_for_signed_in_user()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(await factory.CreateUserAsync(ServerManager.Application.Authorization.Roles.Viewer), ServerManagerWebFactory.DefaultUserPassword, Ct);

        using var keys = await client.GetAsync("/ApiKeys", Ct);
        using var docs = await client.GetAsync("/ApiKeys/Docs", Ct);
        using var all = await client.GetAsync("/ApiKeys/All", Ct);

        Assert.Equal(HttpStatusCode.OK, keys.StatusCode);
        Assert.Equal(HttpStatusCode.OK, docs.StatusCode);
        Assert.Contains("/api/v1/openapi.json", await docs.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, all.StatusCode);
    }

    [Fact]
    public async Task Key_page_creates_key_with_antiforgery_and_shows_it_once()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        using var client = factory.CreateTestClient();
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, Ct);
        var antiforgery = await client.GetAntiforgeryTokenAsync("/ApiKeys", Ct);

        HttpRequestMessage Create(bool withToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/ApiKeys/Create")
            {
                Content = new FormUrlEncodedContent([
                    new("Name", "ci"), new("LifetimeDays", "30"), new("Scopes", Permissions.ServerView)
                ])
            };
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            if (withToken)
                request.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, antiforgery);
            return request;
        }

        using var rejected = await client.SendAsync(Create(false), Ct);
        using var created = await client.SendAsync(Create(true), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        var token = json.RootElement.GetProperty("data").GetProperty("token").GetString()!;
        Assert.StartsWith("smk_", token, StringComparison.Ordinal);

        using var list = await client.GetAsync("/ApiKeys", Ct);
        var html = await list.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(token, html, StringComparison.Ordinal);
        Assert.Contains(token[..16], html, StringComparison.Ordinal);

        using var api = factory.CreateTestClient();
        using var me = await api.SendAsync(ApiRequest(HttpMethod.Get, "/api/v1/me", token), Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Key_cannot_be_given_permissions_the_user_does_not_have()
    {
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.ServerView);
        using var client = factory.CreateTestClient();
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, Ct);
        var antiforgery = await client.GetAntiforgeryTokenAsync("/ApiKeys", Ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/ApiKeys/Create")
        {
            Content = new FormUrlEncodedContent([new("Name", "x"), new("LifetimeDays", "30"), new("Scopes", Permissions.ServerDelete)])
        };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, antiforgery);
        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string Prefix(string token) => token.Substring(4, 12);

    private async Task UpdateKeyAsync(string token, Func<ApplicationDbContext, Task<int>> update)
    {
        using var scope = factory.Services.CreateScope();
        var updated = await update(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        Assert.Equal(1, updated);
        _ = token;
    }
}
