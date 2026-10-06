using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;
using ServerManager.E2E.Tests.Infrastructure;
using ServerManager.Infrastructure.Backups;

namespace ServerManager.E2E.Tests;

/// <summary>
/// "Servisler" modülü gerçek Docker üzerinde: kurulum → sağlıklı → bağlantı → loglar → (PostgreSQL) yedek + indirme → verisiyle kaldırma.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class ManagedServiceTests(E2EFixture fixture)
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromMinutes(3);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Redis_install_health_connection_logs_and_remove_with_data()
    {
        await fixture.RequireAsync();
        var name = E2EFixture.Unique("e2e-redis");
        var service = await InstallAsync(ServiceTemplates.Redis, name);
        var removed = false;
        try
        {
            var secrets = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>().RevealSecretsAsync(service.Id, Ct));
            Assert.True(secrets.IsSuccess, secrets.Message);
            Assert.False(string.IsNullOrEmpty(secrets.Data!.Password));

            // Bağlantı testi: panelde saklanan parola ile container içinden kimlik doğrulamalı PING.
            var ping = await fixture.Shell.RunCheckedAsync(
                $"docker exec -e REDISCLI_AUTH={TestShell.Quote(secrets.Data.Password!)} {service.ContainerName} redis-cli ping", cancellationToken: Ct);
            Assert.Contains("PONG", ping, StringComparison.Ordinal);

            var wrong = await fixture.Shell.RunAsync(
                $"docker exec -e REDISCLI_AUTH=yanlis-parola {service.ContainerName} redis-cli ping", cancellationToken: Ct);
            Assert.DoesNotContain("PONG", wrong.Output, StringComparison.Ordinal);

            await AssertLogsAsync(service.Id, "Ready to accept connections");

            await RemoveAsync(service);
            removed = true;
        }
        finally
        {
            if (!removed)
                await ForceCleanupAsync(service);
        }
    }

    [Fact]
    public async Task Postgres_install_backup_download_and_remove_with_data()
    {
        await fixture.RequireAsync();
        var name = E2EFixture.Unique("e2e-pg");
        var service = await InstallAsync(ServiceTemplates.Postgres, name);
        var removed = false;
        Guid? storageId = null;
        Guid? jobId = null;
        try
        {
            // Bağlantı testi ve yedekte aranacak örnek veri.
            var marker = "e2e_marker_" + Guid.NewGuid().ToString("N")[..8];
            var sql = $"CREATE TABLE e2e_items(id int primary key, note text); INSERT INTO e2e_items VALUES (1, '{marker}'); SELECT note FROM e2e_items;";
            var query = await fixture.Shell.RunCheckedAsync(
                $"docker exec {service.ContainerName} sh -c {TestShell.Quote($"PGPASSWORD=\"$POSTGRES_PASSWORD\" psql -h 127.0.0.1 -U \"$POSTGRES_USER\" -d \"$POSTGRES_DB\" -v ON_ERROR_STOP=1 -tAc \"{sql}\"")}",
                cancellationToken: Ct);
            Assert.Contains(marker, query, StringComparison.Ordinal);

            await AssertLogsAsync(service.Id, "database system is ready to accept connections");

            // Yerel depolama + servis üzerinden yedek işi.
            var folder = E2EFixture.Unique("e2ebk");
            var storage = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IBackupStorageService>().CreateAsync(new BackupStorageFormDto
            {
                Name = folder,
                ProviderSystemName = LocalBackupStorageProvider.ProviderSystemName,
                Settings = new Dictionary<string, string?> { [LocalBackupStorageProvider.FolderKey] = folder }
            }, Ct));
            Assert.True(storage.IsSuccess, E2EFixture.Describe(storage));
            storageId = storage.Data;

            var job = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceBackupService>().CreateJobAsync(service.Id, new ServiceBackupOptionsDto
            {
                StorageId = storageId,
                ScheduleType = BackupScheduleType.Daily,
                ScheduleTime = "03:00",
                KeepLast = 3
            }, Ct));
            Assert.True(job.IsSuccess, E2EFixture.Describe(job));
            jobId = job.Data;

            var jobs = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceBackupService>().ListJobsAsync(service.Id, Ct));
            Assert.Contains(jobs, j => j.Id == jobId);

            var runId = await fixture.AsAdminAsync(async sp =>
            {
                var begin = await sp.GetRequiredService<IBackupRunService>().BeginBackupAsync(jobId!.Value, BackupTrigger.Manual, fixture.BackupActor, Ct);
                Assert.True(begin.IsSuccess, E2EFixture.Describe(begin));
                return begin.Data;
            });

            await fixture.AsAdminAsync(async sp =>
            {
                using var cancellation = new BackupCancellation(Ct);
                var run = await sp.GetRequiredService<IBackupRunService>().RunBackupAsync(runId, fixture.BackupActor, cancellation);
                var details = await sp.GetRequiredService<IBackupRunService>().GetAsync(runId, includeLog: true, Ct);
                Assert.True(run.IsSuccess, $"{run.Message}\n{details.Data?.Log}");
                Assert.Equal(BackupRunStatus.Succeeded, details.Data!.Status);
                Assert.True(details.Data.SizeBytes > 0, details.Data.Log);
                Assert.False(string.IsNullOrEmpty(details.Data.Sha256));
            });

            await fixture.AsAdminAsync(async sp =>
            {
                var download = await sp.GetRequiredService<IBackupDownloadService>().OpenAsync(runId, null, fixture.BackupActor, Ct);
                Assert.True(download.IsSuccess, E2EFixture.Describe(download));
                await using var content = download.Data!.Content;
                using var buffer = new MemoryStream();
                await content.CopyToAsync(buffer, Ct);
                Assert.True(buffer.Length > 0, "İndirilen yedek boş.");
                Assert.False(string.IsNullOrWhiteSpace(download.Data.FileName));
            });

            var deleteArtifact = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IBackupRunService>().DeleteArtifactAsync(runId, fixture.BackupActor, Ct));
            Assert.True(deleteArtifact.IsSuccess, E2EFixture.Describe(deleteArtifact));

            await RemoveAsync(service);
            removed = true;
        }
        finally
        {
            if (jobId is { } id)
                await fixture.AsAdminAsync(sp => sp.GetRequiredService<IBackupJobService>().DeleteAsync(id, CancellationToken.None));
            if (storageId is { } sid)
                await fixture.AsAdminAsync(sp => sp.GetRequiredService<IBackupStorageService>().DeleteAsync(sid, CancellationToken.None));
            if (!removed)
                await ForceCleanupAsync(service);
        }
    }

    private async Task<ManagedServiceDetailsDto> InstallAsync(string templateKey, string name)
    {
        var template = ServiceTemplates.BuiltIn.Single(t => t.Key == templateKey);
        var dto = new CreateManagedServiceDto
        {
            ServerId = fixture.ServerId,
            TemplateKey = template.Key,
            Name = name,
            ImageTag = template.DefaultTag,
            Username = template.Credentials.DefaultUsername,
            Database = template.Credentials.DefaultDatabase,
            // Port yayınlanmaz: paralel koşularda host portu çakışmasın; bağlantı container ağı üzerinden test edilir.
            Ports = template.Ports.Select(p => new ServicePortFormItem { ContainerPort = p.ContainerPort, Publish = false }).ToList()
        };

        var observer = new RecordingObserver();
        var start = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>().BeginInstallAsync(dto, fixture.ServiceActor, Ct));
        Assert.True(start.IsSuccess, E2EFixture.Describe(start));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(OperationTimeout);
        var run = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>()
            .RunOperationAsync(start.Data!.OperationId, fixture.ServiceActor, observer, timeout.Token));
        Assert.True(run.IsSuccess, $"Kurulum başarısız: {run.Message}\n{observer.Tail()}");

        var operation = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>().GetOperationAsync(start.Data!.OperationId, includeLog: false, Ct));
        Assert.Equal(ManagedServiceOperationStatus.Succeeded, operation.Data!.Status);

        var details = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>().GetAsync(start.Data!.ServiceId, Ct));
        Assert.True(details.IsSuccess, details.Message);
        Assert.Equal(ManagedServiceStatus.Running, details.Data!.Status);

        var runtime = await Waiter.UntilAsync(
            () => fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>().GetRuntimeAsync(details.Data.Id, Ct)),
            r => r.IsSuccess && r.Data!.State == "running" && r.Data.Health == "healthy",
            HealthTimeout,
            $"{name} sağlıklı duruma geçmedi",
            TimeSpan.FromSeconds(3));
        Assert.True(runtime.Data!.Exists);

        return details.Data;
    }

    private async Task AssertLogsAsync(Guid serviceId, string expected)
    {
        var logs = await Waiter.UntilAsync(
            () => fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>().GetLogsAsync(serviceId, 500, null, Ct)),
            r => r.IsSuccess && LogText(r.Data!).Contains(expected, StringComparison.Ordinal),
            TimeSpan.FromMinutes(1),
            $"Loglarda '{expected}' bulunamadı");
        Assert.True(logs.IsSuccess);
    }

    private static string LogText(Application.DTOs.Docker.DockerLogsDto logs) =>
        string.Join('\n', logs.Lines.Select(l => l.Text));

    private async Task RemoveAsync(ManagedServiceDetailsDto service)
    {
        var observer = new RecordingObserver();
        var start = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>().BeginRemoveAsync(
            service.Id, new RemoveManagedServiceDto { ConfirmationName = service.Name, RemoveData = true }, fixture.ServiceActor, Ct));
        Assert.True(start.IsSuccess, E2EFixture.Describe(start));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(OperationTimeout);
        var run = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>()
            .RunOperationAsync(start.Data!.OperationId, fixture.ServiceActor, observer, timeout.Token));
        Assert.True(run.IsSuccess, $"Kaldırma başarısız: {run.Message}\n{observer.Tail()}");

        var container = await fixture.Shell.RunAsync($"docker container inspect {service.ContainerName}", cancellationToken: Ct);
        Assert.NotEqual(0, container.ExitCode);

        if (!string.IsNullOrEmpty(service.VolumeName))
        {
            var volume = await fixture.Shell.RunAsync($"docker volume inspect {service.VolumeName}", cancellationToken: Ct);
            Assert.True(volume.ExitCode != 0, $"Volume silinmedi: {service.VolumeName}");
        }

        var list = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IManagedServiceService>().ListAsync(fixture.ServerId, Ct));
        Assert.DoesNotContain(list, s => s.Id == service.Id && s.Status != ManagedServiceStatus.Removed);
    }

    /// <summary>Test yarıda kalırsa sunucuda iz bırakmamak için container ve volume doğrudan silinir.</summary>
    private async Task ForceCleanupAsync(ManagedServiceDetailsDto service)
    {
        await fixture.Shell.TryRunAsync($"docker rm -f {service.ContainerName}");
        if (!string.IsNullOrEmpty(service.VolumeName))
            await fixture.Shell.TryRunAsync($"docker volume rm -f {service.VolumeName}");
    }
}
