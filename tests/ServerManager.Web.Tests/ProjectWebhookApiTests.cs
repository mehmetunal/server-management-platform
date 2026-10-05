using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Deployments;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class ProjectWebhookApiTests(ServerManagerWebFactory factory)
{
    private const string Secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Guid> CreateProjectAsync(bool autoDeploy) => SeedProjectAsync(factory, autoDeploy, null);

    /// <summary>Sunucu ve Compose projesi ekler (SSH'a gidilmez); webhook anahtarı <see cref="Secret"/>.</summary>
    internal static async Task<Guid> SeedProjectAsync(ServerManagerWebFactory factory, bool autoDeploy, string? environment)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var server = new Server { Name = "wh-" + Guid.NewGuid().ToString("N")[..8], Hostname = "wh", IpAddress = "203.0.113.20", Username = "deploy" };
        var slug = "wh-" + Guid.NewGuid().ToString("N")[..10];
        var project = new DeploymentProject
        {
            ServerId = server.Id,
            Name = slug,
            Slug = slug,
            RepositoryUrl = "https://github.com/acme/api.git",
            Branch = "main",
            DeployPath = "/srv/apps/" + slug,
            ComposeFile = "docker-compose.yml",
            AutoDeployOnPush = autoDeploy,
            EncryptedWebhookSecret = protector.Protect(Secret),
            EncryptedEnvironment = environment is null ? null : protector.Protect(environment)
        };
        db.Servers.Add(server);
        db.DeploymentProjects.Add(project);
        await db.SaveChangesAsync(Ct);
        return project.Id;
    }

    private static HttpRequestMessage GitHubRequest(Guid projectId, string eventName, string body, string? secret = Secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/projects/{projectId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(ProjectWebhooks.GitHubEventHeader, eventName);
        if (secret is not null)
        {
            var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
            request.Headers.Add(ProjectWebhooks.GitHubSignatureHeader, "sha256=" + signature);
        }

        return request;
    }

    [Fact]
    public async Task Unknown_project_returns_404()
    {
        using var client = factory.CreateTestClient();
        using var request = GitHubRequest(Guid.NewGuid(), "ping", "{}");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_signature_returns_401_without_cookie_or_antiforgery()
    {
        using var client = factory.CreateTestClient();
        var projectId = await CreateProjectAsync(autoDeploy: true);
        using var request = GitHubRequest(projectId, "push", "{\"ref\":\"refs/heads/main\"}", secret: "wrong-secret");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ping_with_valid_signature_returns_200()
    {
        using var client = factory.CreateTestClient();
        var projectId = await CreateProjectAsync(autoDeploy: true);
        using var request = GitHubRequest(projectId, "ping", "{\"zen\":\"Keep it logically awesome.\"}");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Push_to_another_branch_is_ignored_with_200()
    {
        using var client = factory.CreateTestClient();
        var projectId = await CreateProjectAsync(autoDeploy: true);
        using var request = GitHubRequest(projectId, "push", "{\"ref\":\"refs/heads/feature\",\"after\":\"0123456789abcdef0123456789abcdef01234567\"}");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Push_when_auto_deploy_is_disabled_returns_403()
    {
        using var client = factory.CreateTestClient();
        var projectId = await CreateProjectAsync(autoDeploy: false);
        using var request = GitHubRequest(projectId, "push", "{\"ref\":\"refs/heads/main\",\"after\":\"0123456789abcdef0123456789abcdef01234567\"}");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GitLab_token_is_accepted_and_non_push_events_are_ignored()
    {
        using var client = factory.CreateTestClient();
        var projectId = await CreateProjectAsync(autoDeploy: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/projects/{projectId}")
        {
            Content = new StringContent("{\"object_kind\":\"tag_push\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add(ProjectWebhooks.GitLabEventHeader, "Tag Push Hook");
        request.Headers.Add(ProjectWebhooks.GitLabTokenHeader, Secret);

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
