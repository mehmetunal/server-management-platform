using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.Plugins;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

/// <summary>
/// Örnek "Services.Extra" eklentisi (JSON + C# şablonları) gerçek eklenti klasöründen yüklenir. Eklenti test veritabanında
/// kurulu olmadığı için varsayılan durum devre dışıdır; testler durumu geçici olarak değiştirir ve geri alır.
/// </summary>
[Collection(WebCollection.Name)]
public sealed class ManagedServiceTemplatePluginTests(ServerManagerWebFactory factory)
{
    private const string PluginName = "Services.Extra";
    private const string MeilisearchKey = "services.extra.meilisearch";
    private const string KeycloakKey = "services.extra.keycloak";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IPluginCatalog Plugins => factory.Services.GetRequiredService<IPluginCatalog>();

    private async Task<T> WithPluginStateAsync<T>(bool enabled, Func<Task<T>> action)
    {
        var previous = Plugins.IsEnabled(PluginName);
        Plugins.SetState(PluginName, enabled);
        try
        {
            return await action();
        }
        finally
        {
            Plugins.SetState(PluginName, previous);
        }
    }

    [Fact]
    public void Sample_plugin_templates_load_without_warnings()
    {
        using var client = factory.CreateTestClient();
        var catalog = factory.Services.GetRequiredService<IServiceTemplateCatalog>();

        Assert.Empty(catalog.GetIssues(PluginName));
        Assert.NotNull(catalog.Resolve(MeilisearchKey).Template);
        Assert.NotNull(catalog.Resolve("services.extra.clickhouse").Template);
        Assert.NotNull(catalog.Resolve(KeycloakKey).Template);
    }

    [Fact]
    public async Task Create_page_lists_plugin_templates_only_when_plugin_is_enabled()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);

        var (enabledHtml, wizardStatus, logoStatus) = await WithPluginStateAsync(true, async () =>
        {
            var html = await client.GetStringAsync("/ManagedServices/Create", Ct);
            using var wizard = await client.GetAsync($"/ManagedServices/Create?template={KeycloakKey}", Ct);
            using var logo = await client.GetAsync("/plugins/services.extra/meilisearch.svg", Ct);
            return (html, wizard.StatusCode, logo.StatusCode);
        });
        var disabledHtml = await WithPluginStateAsync(false, () => client.GetStringAsync("/ManagedServices/Create", Ct));

        Assert.Contains("Meilisearch", enabledHtml, StringComparison.Ordinal);
        Assert.Contains("ClickHouse", enabledHtml, StringComparison.Ordinal);
        Assert.Contains("Keycloak", enabledHtml, StringComparison.Ordinal);
        Assert.Contains("Arama ve analitik", enabledHtml, StringComparison.Ordinal);
        Assert.Contains("/plugins/services.extra/meilisearch.svg", enabledHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, wizardStatus);
        Assert.Equal(HttpStatusCode.OK, logoStatus);
        Assert.DoesNotContain(MeilisearchKey, disabledHtml, StringComparison.Ordinal);
        Assert.Contains("PostgreSQL", disabledHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_of_disabled_plugin_shows_warning_and_blocks_settings()
    {
        Guid serviceId;
        using (var scope = factory.Services.CreateScope())
        {
            await ProjectWebhookApiTests.SeedProjectAsync(factory, autoDeploy: false, "A=1\n");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
            var server = await db.Servers.FirstAsync(Ct);
            var slug = "meili" + Guid.NewGuid().ToString("N")[..8];
            var service = new ManagedService
            {
                ServerId = server.Id,
                Name = slug,
                Slug = slug,
                TemplateKey = MeilisearchKey,
                ImageTag = "v1.15",
                ContainerName = ManagedServiceNames.ContainerName(slug),
                EncryptedCredentials = protector.Protect(new ServiceCredentials { EncryptionKey = new string('a', 64) }.ToJson()),
                Status = ManagedServiceStatus.Running
            };
            db.ManagedServices.Add(service);
            await db.SaveChangesAsync(Ct);
            serviceId = service.Id;
        }

        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        var (details, list) = await WithPluginStateAsync(false, async () =>
            (await client.GetStringAsync($"/ManagedServices/Details/{serviceId}", Ct), await client.GetStringAsync("/ManagedServices", Ct)));
        var enabledDetails = await WithPluginStateAsync(true, () => client.GetStringAsync($"/ManagedServices/Details/{serviceId}", Ct));

        Assert.Contains("Şablon eklentisi devre dışı", details, StringComparison.Ordinal);
        Assert.Contains("data-template-blocked", details, StringComparison.Ordinal);
        Assert.DoesNotContain("data-tab=\"settings\"", details, StringComparison.Ordinal);
        Assert.Contains("Şablon eklentisi devre dışı", list, StringComparison.Ordinal);
        Assert.DoesNotContain("data-template-blocked", enabledDetails, StringComparison.Ordinal);
        Assert.Contains("data-tab=\"settings\"", enabledDetails, StringComparison.Ordinal);
    }
}
