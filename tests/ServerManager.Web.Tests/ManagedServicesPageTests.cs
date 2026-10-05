using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class ManagedServicesPageTests(ServerManagerWebFactory factory)
{
    private const string ServicePassword = "Svc-Parola-2026-xyz";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Sunucu, aynı sunucuda Compose projesi ve çalışır durumda PostgreSQL servisi ekler (SSH'a gidilmez).</summary>
    private async Task<(Guid ServiceId, Guid ProjectId, string ServiceName)> SeedAsync()
    {
        var projectId = await ProjectWebhookApiTests.SeedProjectAsync(factory, autoDeploy: false, "EXISTING=1\n");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var project = await db.DeploymentProjects.SingleAsync(p => p.Id == projectId, Ct);
        var slug = "db" + Guid.NewGuid().ToString("N")[..8];
        var service = new ManagedService
        {
            ServerId = project.ServerId,
            Name = slug,
            Slug = slug,
            TemplateKey = ServiceTemplates.Postgres,
            ImageTag = "17",
            ContainerName = ManagedServiceNames.ContainerName(slug),
            EncryptedCredentials = protector.Protect(new ServiceCredentials { Username = "app", Password = ServicePassword, Database = "app" }.ToJson()),
            Status = ManagedServiceStatus.Running
        };
        db.ManagedServices.Add(service);
        await db.SaveChangesAsync(Ct);
        return (service.Id, projectId, slug);
    }

    private static HttpRequestMessage AjaxPost(string path, string? token, IDictionary<string, string>? form = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form ?? new Dictionary<string, string>()) };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("Accept", "application/json");
        if (token is not null)
            request.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, token);
        return request;
    }

    [Theory]
    [InlineData("/ManagedServices")]
    [InlineData("/ManagedServices/Create")]
    [InlineData("/ManagedServices/Create?template=postgres")]
    public async Task Admin_can_open_service_pages(string path)
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);

        using var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Database_wizard_offers_auto_backup_to_admin()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);

        var html = await client.GetStringAsync("/ManagedServices/Create?template=postgres", Ct);
        var app = await client.GetStringAsync("/ManagedServices/Create?template=minio", Ct);

        Assert.Contains("Otomatik yedek", html, StringComparison.Ordinal);
        Assert.Contains("AutoBackup.StorageId", html, StringComparison.Ordinal);
        Assert.DoesNotContain("AutoBackup.StorageId", app, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/ManagedServices")]
    [InlineData("/ManagedServices/Create")]
    public async Task User_without_services_view_is_denied(string path)
    {
        using var client = factory.CreateTestClient();
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.DashboardView, Permissions.ServerView);
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, Ct);

        using var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reveal_requires_permission_and_antiforgery()
    {
        var (serviceId, _, _) = await SeedAsync();

        using var viewer = factory.CreateTestClient();
        await viewer.LoginAsync(await factory.CreateUserAsync(Roles.Viewer), ServerManagerWebFactory.DefaultUserPassword, Ct);
        var viewerToken = await viewer.GetAntiforgeryTokenAsync("/ManagedServices", Ct);
        using var forbidden = await viewer.SendAsync(AjaxPost($"/ManagedServices/Reveal/{serviceId}", viewerToken), Ct);

        using var admin = factory.CreateTestClient();
        await admin.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        using var withoutToken = await admin.SendAsync(AjaxPost($"/ManagedServices/Reveal/{serviceId}", null), Ct);
        var token = await admin.GetAntiforgeryTokenAsync("/ManagedServices", Ct);
        using var revealed = await admin.SendAsync(AjaxPost($"/ManagedServices/Reveal/{serviceId}", token), Ct);
        var body = await revealed.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revealed.StatusCode);
        Assert.Contains(ServicePassword, body, StringComparison.Ordinal);
        Assert.Contains("no-store", revealed.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_details_page_does_not_render_the_password()
    {
        var (serviceId, _, _) = await SeedAsync();
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);

        var html = await client.GetStringAsync($"/ManagedServices/Details/{serviceId}", Ct);
        var preview = await client.SendAsync(HttpClientAuthExtensions.JsonGet($"/ManagedServices/LinkPreview/{serviceId}"), Ct);
        var previewBody = await preview.Content.ReadAsStringAsync(Ct);

        Assert.Contains("Projeye bağla", html, StringComparison.Ordinal);
        Assert.DoesNotContain(ServicePassword, html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Contains("DATABASE_URL", previewBody, StringComparison.Ordinal);
        Assert.DoesNotContain(ServicePassword, previewBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Link_requires_services_manage_and_deployment_manage()
    {
        var (serviceId, projectId, _) = await SeedAsync();
        var form = new Dictionary<string, string> { ["ProjectId"] = projectId.ToString() };

        using var operatorClient = factory.CreateTestClient();
        await operatorClient.LoginAsync(await factory.CreateUserAsync(Roles.Operator), ServerManagerWebFactory.DefaultUserPassword, Ct);
        var operatorToken = await operatorClient.GetAntiforgeryTokenAsync("/ManagedServices", Ct);
        using var operatorResponse = await operatorClient.SendAsync(AjaxPost($"/ManagedServices/Link/{serviceId}", operatorToken, form), Ct);

        using var deployer = factory.CreateTestClient();
        var deployerEmail = await factory.CreateUserWithPermissionsAsync(Permissions.ServicesView, Permissions.DeploymentView, Permissions.DeploymentManage);
        await deployer.LoginAsync(deployerEmail, ServerManagerWebFactory.DefaultUserPassword, Ct);
        var deployerToken = await deployer.GetAntiforgeryTokenAsync("/ManagedServices", Ct);
        using var deployerResponse = await deployer.SendAsync(AjaxPost($"/ManagedServices/Link/{serviceId}", deployerToken, form), Ct);

        using var admin = factory.CreateTestClient();
        await admin.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        using var withoutToken = await admin.SendAsync(AjaxPost($"/ManagedServices/Link/{serviceId}", null, form), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, operatorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deployerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
    }

    [Fact]
    public async Task Linked_service_is_listed_on_project_page_and_env_keys_are_written()
    {
        var (serviceId, projectId, serviceName) = await SeedAsync();
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var token = await client.GetAntiforgeryTokenAsync("/ManagedServices", Ct);

        using var link = await client.SendAsync(AjaxPost($"/ManagedServices/Link/{serviceId}", token, new Dictionary<string, string>
        {
            ["ProjectId"] = projectId.ToString(),
            ["Variables[0].SourceKey"] = "DATABASE_URL",
            ["Variables[0].Key"] = "APP_DB_URL",
            ["Variables[0].Include"] = "true",
            ["Variables[1].SourceKey"] = "PGHOST",
            ["Variables[1].Key"] = "PGHOST",
            ["Variables[1].Include"] = "false"
        }), Ct);
        var linkBody = await link.Content.ReadAsStringAsync(Ct);
        using var again = await client.SendAsync(AjaxPost($"/ManagedServices/Link/{serviceId}", token, new Dictionary<string, string> { ["ProjectId"] = projectId.ToString() }), Ct);
        var html = await client.GetStringAsync($"/Projects/Details/{projectId}", Ct);

        Assert.True(link.StatusCode == HttpStatusCode.OK, linkBody);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("Bağlı servisler", html, StringComparison.Ordinal);
        Assert.Contains(serviceName, html, StringComparison.Ordinal);
        Assert.Contains("APP_DB_URL", html, StringComparison.Ordinal);
        Assert.DoesNotContain(ServicePassword, html, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var project = await db.DeploymentProjects.AsNoTracking().SingleAsync(p => p.Id == projectId, Ct);
        var environment = protector.Unprotect(project.EncryptedEnvironment!);
        Assert.Contains("EXISTING=1", environment, StringComparison.Ordinal);
        Assert.Contains($"APP_DB_URL=postgres://app:{ServicePassword}@sm-svc-{serviceName}:5432/app", environment, StringComparison.Ordinal);
        Assert.DoesNotContain("PGHOST", environment, StringComparison.Ordinal);
        Assert.True(await db.ProjectServiceLinks.AnyAsync(l => l.ProjectId == projectId && l.ManagedServiceId == serviceId, Ct));
    }
}
