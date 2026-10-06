using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.Backups;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Notifications;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Backups;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class BackupDownloadTests(ServerManagerWebFactory factory)
{
    private const string Passphrase = "web-testi-parolasi-2026";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Yerel depolama hedefi, diskte yedek dosyası ve buna ait başarılı yedek kaydı ekler (SSH'a gidilmez).</summary>
    private async Task<(Guid RunId, byte[] Stored)> SeedAsync(
        byte[] content, bool encrypted = false, BackupRunStatus status = BackupRunStatus.Succeeded, string? sha256 = null)
    {
        var folder = "dl" + Guid.NewGuid().ToString("N")[..10];
        var jobId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var startedAt = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var objectKey = BackupNames.ObjectKey(jobId, runId, startedAt, BackupSourceType.DockerVolume, encrypted);

        var path = Path.Combine(factory.BackupRootPath, folder, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, Ct);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var storage = new BackupStorage
        {
            Name = folder,
            ProviderSystemName = LocalBackupStorageProvider.ProviderSystemName,
            EncryptedSettings = protector.Protect(NotificationSettingsSerializer.Serialize(
                new Dictionary<string, string> { [LocalBackupStorageProvider.FolderKey] = folder }))
        };
        var server = new Server { Name = folder, Hostname = "bk", IpAddress = "203.0.113.30", Username = "deploy" };
        db.Servers.Add(server);
        db.BackupStorages.Add(storage);
        db.BackupJobs.Add(new BackupJob
        {
            Id = jobId,
            Name = "volume-yedegi-" + folder,
            ServerId = server.Id,
            StorageId = storage.Id,
            SourceType = BackupSourceType.DockerVolume,
            VolumeName = "app-data",
            EncryptionEnabled = encrypted,
            LastRunAt = startedAt,
            LastRunStatus = status
        });
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Operation = BackupOperation.Backup,
            Status = status,
            JobId = jobId,
            JobName = "volume-yedegi",
            SourceType = BackupSourceType.DockerVolume,
            ServerId = server.Id,
            ServerName = server.Name,
            StorageId = storage.Id,
            StorageName = storage.Name,
            ObjectKey = objectKey,
            FileName = BackupNames.FileName("volume-yedegi", startedAt, BackupSourceType.DockerVolume, encrypted),
            SizeBytes = content.Length,
            Sha256 = sha256 ?? Convert.ToHexStringLower(SHA256.HashData(content)),
            IsEncrypted = encrypted,
            StartedAt = startedAt,
            CompletedAt = startedAt.AddMinutes(1)
        });
        await db.SaveChangesAsync(Ct);
        _lastSeed = (jobId, server.Id);
        return (runId, content);
    }

    private (Guid JobId, Guid ServerId) _lastSeed;

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, Ct);
        return client;
    }

    [Fact]
    public async Task Download_requires_backup_download_permission()
    {
        var (runId, _) = await SeedAsync(RandomNumberGenerator.GetBytes(64));
        using var client = factory.CreateTestClient();
        var email = await factory.CreateUserWithPermissionsAsync(Permissions.DashboardView, Permissions.BackupView, Permissions.BackupRestore);
        await client.LoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, Ct);

        using var response = await client.GetAsync($"/BackupRuns/Download/{runId}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Viewer_role_cannot_download()
    {
        var (runId, _) = await SeedAsync(RandomNumberGenerator.GetBytes(64));
        using var client = factory.CreateTestClient();
        await client.LoginAsync(await factory.CreateUserAsync(Roles.Viewer), ServerManagerWebFactory.DefaultUserPassword, Ct);

        using var response = await client.GetAsync($"/BackupRuns/Download/{runId}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_run_returns_404()
    {
        using var client = await AdminAsync();

        using var response = await client.GetAsync($"/BackupRuns/Download/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Local_artifact_is_streamed_with_download_headers_and_audited()
    {
        var (runId, stored) = await SeedAsync(RandomNumberGenerator.GetBytes(300_000));
        using var client = await AdminAsync();

        using var response = await client.GetAsync($"/BackupRuns/Download/{runId}", HttpCompletionOption.ResponseHeadersRead, Ct);
        var body = await response.Content.ReadAsByteArrayAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(stored, body);
        Assert.Equal("application/gzip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(stored.Length, response.Content.Headers.ContentLength);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("volume-yedegi-20261005-120000.tar.gz", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.True(response.Headers.CacheControl?.NoStore);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == AuditActions.BackupDownload && a.EntityId == runId.ToString(), Ct);
        Assert.True(audit.IsSuccess);
        Assert.Contains(runId.ToString(), audit.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Operator_role_can_download_by_default()
    {
        var (runId, stored) = await SeedAsync(RandomNumberGenerator.GetBytes(128));
        using var client = factory.CreateTestClient();
        await client.LoginAsync(await factory.CreateUserAsync(Roles.Operator), ServerManagerWebFactory.DefaultUserPassword, Ct);

        using var response = await client.GetAsync($"/BackupRuns/Download/{runId}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(stored, await response.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task Failed_run_redirects_back_to_details_with_error()
    {
        var (runId, _) = await SeedAsync(RandomNumberGenerator.GetBytes(64), status: BackupRunStatus.Failed);
        using var client = await AdminAsync();

        using var response = await client.GetAsync($"/BackupRuns/Download/{runId}", Ct);
        var details = await client.GetStringAsync($"/BackupRuns/Details/{runId}", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/BackupRuns/Details/{runId}", response.Headers.Location!.OriginalString);
        Assert.Contains("Yalnızca başarılı yedekler indirilebilir.", WebUtility.HtmlDecode(details), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encrypted_artifact_downloads_as_is_or_decrypted_with_passphrase()
    {
        var plain = RandomNumberGenerator.GetBytes(BackupEncryption.ChunkSize + 1000);
        using var encryptedStream = new MemoryStream();
        await BackupEncryption.EncryptAsync(new MemoryStream(plain), encryptedStream, Passphrase, BackupEncryption.MinIterations, Ct);
        var (runId, stored) = await SeedAsync(encryptedStream.ToArray(), encrypted: true);
        using var client = await AdminAsync();

        using var asIs = await client.GetAsync($"/BackupRuns/Download/{runId}", Ct);
        Assert.Equal(HttpStatusCode.OK, asIs.StatusCode);
        Assert.Equal(stored, await asIs.Content.ReadAsByteArrayAsync(Ct));
        Assert.EndsWith(".tar.gz.smbk", asIs.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);

        var token = await client.GetAntiforgeryTokenAsync($"/BackupRuns/Details/{runId}", Ct);

        using var wrong = await client.SendAsync(DecryptRequest(runId, token, "yanlis-parola-0000"), Ct);
        Assert.Equal(HttpStatusCode.Redirect, wrong.StatusCode);
        var details = await client.GetStringAsync($"/BackupRuns/Details/{runId}", Ct);
        Assert.Contains("parola yanlış", WebUtility.HtmlDecode(details), StringComparison.Ordinal);

        using var decrypted = await client.SendAsync(DecryptRequest(runId, token, Passphrase), Ct);
        Assert.Equal(HttpStatusCode.OK, decrypted.StatusCode);
        Assert.Equal(plain, await decrypted.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal(plain.Length, decrypted.Content.Headers.ContentLength);
        Assert.Equal("volume-yedegi-20261005-120000.tar.gz", decrypted.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.True(decrypted.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Backup_pages_link_to_the_download()
    {
        var (runId, _) = await SeedAsync(RandomNumberGenerator.GetBytes(64));
        var (jobId, serverId) = _lastSeed;
        var (failedRunId, _) = await SeedAsync(RandomNumberGenerator.GetBytes(64), status: BackupRunStatus.Failed);
        var failedJobId = _lastSeed.JobId;
        using var client = await AdminAsync();
        var link = $"/BackupRuns/Download/{runId}";

        foreach (var page in new[]
                 {
                     "/BackupJobs", $"/BackupJobs/Details/{jobId}", $"/BackupRuns?jobId={jobId}", $"/BackupRuns/Details/{runId}",
                     $"/BackupRuns/Restore/{runId}", $"/ServerBackups/Index/{serverId}"
                 })
        {
            using var response = await client.GetAsync(page, Ct);
            var html = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{page}: {(int)response.StatusCode}");
            Assert.True(html.Contains(link, StringComparison.Ordinal), $"{page} sayfasında indirme bağlantısı yok.");
        }

        var failedJob = WebUtility.HtmlDecode(await client.GetStringAsync($"/BackupJobs/Details/{failedJobId}", Ct));
        Assert.DoesNotContain($"/BackupRuns/Download/{failedRunId}", failedJob, StringComparison.Ordinal);
        Assert.Contains("Yalnızca başarılı yedekler indirilebilir.", failedJob, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decrypted_download_requires_antiforgery_token()
    {
        var (runId, _) = await SeedAsync(RandomNumberGenerator.GetBytes(64), encrypted: true);
        using var client = await AdminAsync();

        using var response = await client.SendAsync(DecryptRequest(runId, null, Passphrase), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpRequestMessage DecryptRequest(Guid runId, string? token, string passphrase)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/BackupRuns/Download/{runId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["passphrase"] = passphrase })
        };
        if (token is not null)
            request.Headers.Add(HttpClientAuthExtensions.AntiforgeryHeader, token);
        return request;
    }
}
