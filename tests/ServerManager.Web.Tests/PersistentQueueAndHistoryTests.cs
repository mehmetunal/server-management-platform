using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Alerting;
using ServerManager.Application.Deployments;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.ResourceUsage;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

/// <summary>Kalıcı kuyrukların ve kaynak geçmişi / servis alarmı sorgularının gerçek SQL Server'da gidiş-dönüşü.</summary>
[Collection(WebCollection.Name)]
public sealed class PersistentQueueAndHistoryTests(ServerManagerWebFactory factory)
{
    private const string Secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Server> SeedServerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = new Server { Name = "rh-" + Guid.NewGuid().ToString("N")[..8], Hostname = "rh", IpAddress = "203.0.113.30", Username = "ops" };
        db.Servers.Add(server);
        await db.SaveChangesAsync(Ct);
        return server;
    }

    [Fact]
    public async Task Push_during_running_deployment_is_queued_in_the_database()
    {
        using var client = factory.CreateTestClient();
        var projectId = await ProjectWebhookApiTests.SeedProjectAsync(factory, autoDeploy: true, environment: null);

        // Başka bir örnekte (veya kayıtta) süren deployment: bu örneğin belleğinde yok, yalnızca veritabanında.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = await db.DeploymentProjects.SingleAsync(p => p.Id == projectId, Ct);
            db.Deployments.Add(new Deployment
            {
                ProjectId = projectId, ProjectName = project.Name, ServerId = project.ServerId, ServerName = "wh",
                Branch = "main", Status = DeploymentStatus.Building
            });
            await db.SaveChangesAsync(Ct);
        }

        var body = $"{{\"ref\":\"refs/heads/main\",\"after\":\"{Commit}\"}}";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/projects/{projectId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(ProjectWebhooks.GitHubEventHeader, "push");
        request.Headers.Add(ProjectWebhooks.GitHubSignatureHeader,
            "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body))));

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        var repository = verify.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var pending = Assert.Single(await repository.ListPendingWebhookDeploysAsync(Ct), p => p.ProjectId == projectId);
        Assert.Equal(Commit, pending.Commit);

        // Daha yeni bir kayıt varken eski zamana kadar temizleme kaydı silmez; kendi zamanıyla siler.
        Assert.False(await repository.ClearPendingWebhookDeployAsync(projectId, pending.QueuedAt.AddMinutes(-1), Ct));
        Assert.True(await repository.ClearPendingWebhookDeployAsync(projectId, pending.QueuedAt, Ct));
        Assert.DoesNotContain(await repository.ListPendingWebhookDeploysAsync(Ct), p => p.ProjectId == projectId);
    }

    [Fact]
    public async Task Auto_backup_request_is_stored_on_operation_and_claimed_once()
    {
        using var client = factory.CreateTestClient();
        var server = await SeedServerAsync();
        Guid operationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
            var slug = "ab" + Guid.NewGuid().ToString("N")[..8];
            var service = new ManagedService
            {
                ServerId = server.Id, Name = slug, Slug = slug, TemplateKey = "postgres", ImageTag = "17",
                ContainerName = "sm-svc-" + slug, EncryptedCredentials = protector.Protect("{}"), Status = ManagedServiceStatus.Running
            };
            var operation = new ManagedServiceOperation
            {
                ServiceId = service.Id, ServerId = server.Id, ServiceName = slug, Kind = ManagedServiceOperationKind.Install,
                Status = ManagedServiceOperationStatus.Running, UserName = "ayse"
            };
            db.ManagedServices.Add(service);
            await db.SaveChangesAsync(Ct);
            db.ManagedServiceOperations.Add(operation);
            await db.SaveChangesAsync(Ct);
            operationId = operation.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IManagedServiceRepository>();
            Assert.True(await repository.SetPendingAutoBackupAsync(operationId, "enc:payload", Ct));

            // Süren işlem bekleyenler listesinde değildir (açılışta yalnızca bitenler işlenir).
            Assert.DoesNotContain(await repository.ListPendingAutoBackupOperationsAsync(Ct), o => o.Id == operationId);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.ManagedServiceOperations.Where(o => o.Id == operationId)
                .ExecuteUpdateAsync(set => set.SetProperty(o => o.Status, ManagedServiceOperationStatus.Succeeded), Ct);

            var pending = Assert.Single(await repository.ListPendingAutoBackupOperationsAsync(Ct), o => o.Id == operationId);
            Assert.Equal("ayse", pending.UserName);

            Assert.Equal("enc:payload", await repository.ClaimPendingAutoBackupAsync(operationId, Ct));
            Assert.Null(await repository.ClaimPendingAutoBackupAsync(operationId, Ct));
            Assert.DoesNotContain(await repository.ListPendingAutoBackupOperationsAsync(Ct), o => o.Id == operationId);
        }
    }

    [Fact]
    public async Task Resource_history_round_trips_and_feeds_service_alerts()
    {
        using var client = factory.CreateTestClient();
        var server = await SeedServerAsync();
        var now = DateTime.UtcNow;
        var hourAgo = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(-2);

        Guid serviceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
            var service = new ManagedService
            {
                ServerId = server.Id, Name = "cache", Slug = "cache" + Guid.NewGuid().ToString("N")[..6], TemplateKey = "redis", ImageTag = "7",
                ContainerName = "sm-svc-cache", EncryptedCredentials = protector.Protect("{}"), Status = ManagedServiceStatus.Running
            };
            db.ManagedServices.Add(service);
            await db.SaveChangesAsync(Ct);
            serviceId = service.Id;

            var repository = scope.ServiceProvider.GetRequiredService<IResourceHistoryRepository>();
            await repository.AddSamplesAsync(
            [
                Sample(server.Id, hourAgo.AddMinutes(5), "sm-svc-cache", "running", 0, 10, 100),
                Sample(server.Id, hourAgo.AddMinutes(10), "sm-svc-cache", "running", 1, 30, 300),
                Sample(server.Id, now.AddMinutes(-2), "sm-svc-cache", "exited", 3, 0, 0),
                Sample(server.Id, now.AddMinutes(-2), "web", "running", 0, 50, 1000)
            ], Ct);
            await repository.AddProcessSnapshotAsync(new ProcessSnapshot
            {
                ServerId = server.Id, CollectedAt = now.AddMinutes(-2), CpuBusyPercent = 42,
                ProcessesJson = ResourceHistoryRules.SerializeProcesses([new ProcessSample(1, "root", 80, 1, 2048, "ffmpeg", "ffmpeg -i x")])
            }, Ct);
            await repository.SaveChangesAsync(Ct);

            Assert.Equal(["sm-svc-cache"], await repository.GetManagedContainerNamesAsync(server.Id, Ct));
            Assert.True(await repository.AggregateHourlyAsync(hourAgo.AddHours(1), Ct) >= 1);
            Assert.Equal(0, await repository.AggregateHourlyAsync(hourAgo.AddHours(1), Ct));

            var raw = await repository.GetSeriesAsync(server.Id, now.AddHours(-3), now, 300, hourly: false, Ct);
            Assert.Contains(raw, p => p.ContainerName == "web" && p.CpuPercent == 50);
            var hourly = await repository.GetSeriesAsync(server.Id, now.AddDays(-3), now, 3600, hourly: true, Ct);
            var cacheHour = Assert.Single(hourly, p => p.ContainerName == "sm-svc-cache");
            Assert.Equal(20, cacheHour.CpuPercent);

            var summaries = await repository.GetContainerSummariesAsync(server.Id, now.AddHours(-3), now, hourly: false, Ct);
            var cache = Assert.Single(summaries, s => s.Name == "sm-svc-cache");
            Assert.Equal((3, 30.0, 3), (cache.Samples, cache.CpuMax, cache.Restarts));
            Assert.NotNull((await repository.GetContainerSummariesAsync(server.Id, now.AddDays(-3), now, hourly: true, Ct)).SingleOrDefault(s => s.Name == "sm-svc-cache"));

            var snapshot = await repository.GetNearestProcessSnapshotAsync(server.Id, now, Ct);
            Assert.Equal("ffmpeg", Assert.Single(ResourceHistoryRules.DeserializeProcesses(snapshot!.ProcessesJson)).Name);
            Assert.Single(await repository.GetProcessSnapshotsAsync(server.Id, now.AddHours(-1), now, 10, Ct));

            await repository.UpsertReclaimableAsync(new ServerReclaimableSpace { ServerId = server.Id, ScannedAt = now, ReclaimableBytes = 5, SafeReclaimableBytes = 1 }, Ct);
            await repository.UpsertReclaimableAsync(new ServerReclaimableSpace { ServerId = server.Id, ScannedAt = now, ReclaimableBytes = 50, SafeReclaimableBytes = 10 }, Ct);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var alerts = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
            var samples = await alerts.GetContainerSamplesAsync(now.AddHours(-3), [server.Id], Ct);
            Assert.Equal(4, samples.Count);

            var service = Assert.Single(await alerts.GetManagedServiceSnapshotsAsync(Ct), s => s.ServiceId == serviceId);
            Assert.Equal(server.Name, service.ServerName);

            var reclaimable = Assert.Single(await alerts.GetReclaimableSnapshotsAsync(Ct), s => s.ServerId == server.Id);
            Assert.Equal(50, reclaimable.ReclaimableBytes);

            var rule = new AlertRule { Name = "servis", Kind = AlertRuleKind.ServiceDown, ManagedServiceId = serviceId };
            var condition = Assert.Single(AlertConditionEvaluator.ForServiceDown(rule, [service], samples, now, TimeSpan.FromMinutes(15)));
            Assert.Equal(AlertConditionState.Firing, condition.State);

            Assert.Empty(await alerts.GetOpenServiceEventsAsync(serviceId, AlertRuleKinds.ContainerTargetKey(server.Id, "sm-svc-cache"), Ct));
        }
    }

    [Fact]
    public async Task History_endpoints_answer_for_known_server()
    {
        var server = await SeedServerAsync();
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);

        using var history = await client.GetAsync($"/ServerResources/History/{server.Id}?range=6h", Ct);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Contains("\"source\":\"raw\"", await history.Content.ReadAsStringAsync(Ct));

        using var snapshot = await client.GetAsync($"/ServerResources/ProcessSnapshot/{server.Id}?at=2026-10-06T03:00:00Z", Ct);
        Assert.Equal(HttpStatusCode.NotFound, snapshot.StatusCode);

        using var page = await client.GetAsync($"/AlertRules/Create?kind={AlertRuleKind.ServiceDown}", Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("data-rule-service", await page.Content.ReadAsStringAsync(Ct));
    }

    private static ContainerMetricSample Sample(Guid serverId, DateTime at, string name, string state, int restarts, double cpu, long memory) => new()
    {
        ServerId = serverId, CollectedAt = at, ContainerName = name, State = state, RestartCount = restarts, CpuPercent = cpu, MemoryUsageBytes = memory
    };
}
