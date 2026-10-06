using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces.Backups;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Backups;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class BackupDownloadServiceTests
{
    private const string Passphrase = "indirme-parolasi-123";

    private readonly IBackupRepository _repository = Substitute.For<IBackupRepository>();
    private readonly IBackupStorageService _storageService = Substitute.For<IBackupStorageService>();
    private readonly IBackupStorageProvider _provider = Substitute.For<IBackupStorageProvider>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly BackupStorage _storage = new() { Name = "yerel" };
    private readonly BackupActor _actor = new("u1", "ayse", "10.0.0.1");
    private readonly BackupDownloadService _service;

    public BackupDownloadServiceTests()
    {
        _provider.DisplayName.Returns("Yerel disk");
        _repository.GetStorageIncludingDeletedAsync(_storage.Id, Arg.Any<CancellationToken>()).Returns(_storage);
        _storageService.Resolve(_storage).Returns(ServiceResult<BackupStorageTarget>.Success(
            new BackupStorageTarget(_storage.Id, _storage.Name, _provider, new Dictionary<string, string>())));
        _service = new BackupDownloadService(_repository, _storageService, _audit, NullLogger<BackupDownloadService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private BackupRun Run(bool encrypted = false, BackupRunStatus status = BackupRunStatus.Succeeded, long? size = null, string? sha = null)
    {
        var run = new BackupRun
        {
            Operation = BackupOperation.Backup,
            Status = status,
            JobId = Guid.NewGuid(),
            JobName = "site",
            SourceType = BackupSourceType.DockerVolume,
            StorageId = _storage.Id,
            StorageName = _storage.Name,
            IsEncrypted = encrypted,
            SizeBytes = size,
            Sha256 = sha,
            FileName = encrypted ? "site-20261005-120000.tar.gz.smbk" : "site-20261005-120000.tar.gz"
        };
        run.ObjectKey = $"{run.JobId:N}/20261005-120000-{run.Id.ToString("N")[..8]}.tar.gz{(encrypted ? ".smbk" : "")}";
        _repository.GetRunAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        return run;
    }

    private void Stored(BackupRun run, Stream content) =>
        _provider.OpenReadAsync(Arg.Any<IReadOnlyDictionary<string, string>>(), run.ObjectKey!, Arg.Any<CancellationToken>())
            .Returns(_ => ServiceResult<Stream>.Success(content));

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        await using (stream)
        {
            using var output = new MemoryStream();
            await stream.CopyToAsync(output, Ct);
            return output.ToArray();
        }
    }

    [Fact]
    public async Task Missing_run_is_not_found()
    {
        var result = await _service.OpenAsync(Guid.NewGuid(), null, _actor, Ct);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        await _audit.DidNotReceive().LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(BackupRunStatus.Running)]
    [InlineData(BackupRunStatus.Failed)]
    [InlineData(BackupRunStatus.Cancelled)]
    public async Task Unsuccessful_run_is_rejected_without_touching_storage(BackupRunStatus status)
    {
        var run = Run(status: status);

        var result = await _service.OpenAsync(run.Id, null, _actor, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        await _provider.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task Artifact_deleted_by_retention_is_rejected()
    {
        var run = Run();
        run.ArtifactDeletedAt = DateTime.UtcNow;
        run.ArtifactDeletedBy = BackupRunService.RetentionActor;

        var result = await _service.OpenAsync(run.Id, null, _actor, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains(BackupRunService.RetentionActor, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_run_has_nothing_to_download()
    {
        var run = Run();
        run.Operation = BackupOperation.Restore;

        var result = await _service.OpenAsync(run.Id, null, _actor, Ct);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Passphrase_for_unencrypted_backup_is_rejected()
    {
        var run = Run();

        var result = await _service.OpenAsync(run.Id, Passphrase, _actor, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _provider.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task Storage_that_cannot_open_the_file_returns_turkish_error_and_is_audited()
    {
        var run = Run();
        _provider.OpenReadAsync(Arg.Any<IReadOnlyDictionary<string, string>>(), run.ObjectKey!, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<Stream>.Failure("Yedek dosyası depolamada bulunamadı."));

        var result = await _service.OpenAsync(run.Id, null, _actor, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("depolamadan okunamadı", result.Message, StringComparison.Ordinal);
        await _audit.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.BackupDownload && !e.IsSuccess), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provider_without_read_support_returns_clear_error()
    {
        var run = Run();
        _provider.OpenReadAsync(Arg.Any<IReadOnlyDictionary<string, string>>(), run.ObjectKey!, Arg.Any<CancellationToken>())
            .Returns<Task<ServiceResult<Stream>>>(_ => throw new NotSupportedException());

        var result = await _service.OpenAsync(run.Id, null, _actor, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("desteklemiyor", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unavailable_storage_plugin_returns_error()
    {
        var run = Run();
        _storageService.Resolve(_storage).Returns(ServiceResult<BackupStorageTarget>.Failure("Eklenti devre dışı."));

        var result = await _service.OpenAsync(run.Id, null, _actor, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("Eklenti devre dışı.", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plain_backup_is_streamed_as_is_with_length_and_audit()
    {
        var content = RandomNumberGenerator.GetBytes(4096);
        var run = Run(size: content.Length);
        Stored(run, new BackupDownloadsTests.NonSeekableStream(content));

        var result = await _service.OpenAsync(run.Id, null, _actor, Ct);

        Assert.True(result.IsSuccess);
        var download = result.Data!;
        Assert.Equal("site-20261005-120000.tar.gz", download.FileName);
        Assert.Equal("application/gzip", download.ContentType);
        Assert.Equal(content.Length, download.Length);
        Assert.Equal(content, await ReadAllAsync(download.Content));
        await _audit.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.BackupDownload && e.IsSuccess && e.EntityId == run.Id.ToString()
                                    && e.TargetName == "site" && e.UserNameOverride == "ayse" && e.Details!.Contains("4,0 KB")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Encrypted_backup_without_passphrase_is_streamed_encrypted()
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));
        var run = Run(encrypted: true, size: encrypted.Length);
        Stored(run, new MemoryStream(encrypted));

        var result = await _service.OpenAsync(run.Id, null, _actor, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("site-20261005-120000.tar.gz.smbk", result.Data!.FileName);
        Assert.Equal("application/octet-stream", result.Data.ContentType);
        Assert.Equal(encrypted, await ReadAllAsync(result.Data.Content));
    }

    [Fact]
    public async Task Encrypted_backup_with_correct_passphrase_is_decrypted_while_streaming()
    {
        var plain = RandomNumberGenerator.GetBytes(BackupEncryption.ChunkSize + 333);
        var encrypted = await EncryptAsync(plain);
        var run = Run(encrypted: true, size: encrypted.Length, sha: Convert.ToHexStringLower(SHA256.HashData(encrypted)));
        Stored(run, new BackupDownloadsTests.NonSeekableStream(encrypted));

        var result = await _service.OpenAsync(run.Id, Passphrase, _actor, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("site-20261005-120000.tar.gz", result.Data!.FileName);
        Assert.Equal("application/gzip", result.Data.ContentType);
        Assert.Equal(plain.Length, result.Data.Length);
        Assert.Equal(plain, await ReadAllAsync(result.Data.Content));
    }

    [Fact]
    public async Task Wrong_passphrase_is_reported_before_streaming_and_audited_as_failure()
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));
        var run = Run(encrypted: true, size: encrypted.Length);
        var stored = new BackupDownloadsTests.NonSeekableStream(encrypted);
        Stored(run, stored);

        var result = await _service.OpenAsync(run.Id, "yanlis-parola-000", _actor, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains("parola yanlış", result.Message, StringComparison.Ordinal);
        Assert.True(stored.IsDisposed);
        await _audit.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.BackupDownload && !e.IsSuccess), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tampered_file_fails_the_hash_check_at_the_end()
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));
        var run = Run(encrypted: true, size: encrypted.Length, sha: new string('0', 64));
        Stored(run, new MemoryStream(encrypted));

        var result = await _service.OpenAsync(run.Id, Passphrase, _actor, Ct);

        Assert.True(result.IsSuccess);
        await Assert.ThrowsAsync<BackupFormatException>(() => ReadAllAsync(result.Data!.Content));
    }

    private static async Task<byte[]> EncryptAsync(byte[] plain)
    {
        using var output = new MemoryStream();
        await BackupEncryption.EncryptAsync(new MemoryStream(plain), output, Passphrase, BackupEncryption.MinIterations, Ct);
        return output.ToArray();
    }
}
