using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

/// <summary>Kalıcı kuyruklar: webhook takip deploy'u ve kurulum sonrası otomatik yedek isteği.</summary>
public class PersistentQueueTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 3, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- Webhook takip deploy'u

    private static ProjectWebhookService WebhookService(IDeploymentRepository repository) => new(
        repository,
        new FakeSecretProtector(),
        Substitute.For<IAuditLogService>(),
        Substitute.For<ICurrentUserService>(),
        new FixedTimeProvider(Now),
        NullLogger<ProjectWebhookService>.Instance);

    [Fact]
    public async Task Follow_up_is_written_to_the_project_row_with_commit_and_ip()
    {
        var repository = Substitute.For<IDeploymentRepository>();
        var projectId = Guid.NewGuid();
        repository.SetPendingWebhookDeployAsync(projectId, Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(true);

        var pending = await WebhookService(repository).QueueFollowUpAsync(projectId, " abc123 ", "203.0.113.5", Ct);

        Assert.NotNull(pending);
        Assert.Equal(Now.UtcDateTime, pending.QueuedAt);
        Assert.Equal("abc123", pending.Commit);
        await repository.Received(1).SetPendingWebhookDeployAsync(projectId, Now.UtcDateTime, "abc123", "203.0.113.5", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Follow_up_for_missing_project_is_not_queued()
    {
        var repository = Substitute.For<IDeploymentRepository>();
        repository.SetPendingWebhookDeployAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(false);

        Assert.Null(await WebhookService(repository).QueueFollowUpAsync(Guid.NewGuid(), null, null, Ct));
    }

    [Fact]
    public async Task Completing_follow_up_clears_only_up_to_its_queue_time()
    {
        var repository = Substitute.For<IDeploymentRepository>();
        var pending = new PendingWebhookDeploy(Guid.NewGuid(), Now.UtcDateTime.AddMinutes(-3), "c", null);
        repository.ClearPendingWebhookDeployAsync(pending.ProjectId, pending.QueuedAt, Arg.Any<CancellationToken>()).Returns(true);

        Assert.True(await WebhookService(repository).CompleteFollowUpAsync(pending, Ct));
        await repository.Received(1).ClearPendingWebhookDeployAsync(pending.ProjectId, pending.QueuedAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Webhook_status_shows_pending_follow_up()
    {
        var repository = Substitute.For<IDeploymentRepository>();
        var project = new DeploymentProject { Name = "api", PendingWebhookDeployAt = Now.UtcDateTime, PendingWebhookCommit = "deadbeef" };
        repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var status = await WebhookService(repository).GetAsync(project.Id, Ct);

        Assert.Equal(Now.UtcDateTime, status.Data!.PendingDeployAt);
        Assert.Equal("deadbeef", status.Data.PendingDeployCommit);
    }

    // ---- Kurulum sonrası otomatik yedek

    private readonly IManagedServiceRepository _services = Substitute.For<IManagedServiceRepository>();
    private readonly IManagedServiceBackupService _backups = Substitute.For<IManagedServiceBackupService>();
    private readonly FakeSecretProtector _protector = new();

    private ServiceAutoBackupQueue Queue() => new(_services, _backups, _protector, NullLogger<ServiceAutoBackupQueue>.Instance);

    private static ServiceBackupOptionsDto Options() => new()
    {
        Enabled = true,
        StorageId = Guid.NewGuid(),
        ScheduleType = BackupScheduleType.Daily,
        ScheduleTime = "02:30",
        KeepLast = 5,
        EncryptionPassphrase = "çok-gizli-parola-123"
    };

    private ManagedServiceOperation Operation(ManagedServiceOperationStatus status)
    {
        var operation = new ManagedServiceOperation { ServiceId = Guid.NewGuid(), Kind = ManagedServiceOperationKind.Install, Status = status };
        _services.GetOperationAsync(operation.Id, Arg.Any<CancellationToken>()).Returns(operation);
        return operation;
    }

    [Fact]
    public async Task Enqueue_stores_encrypted_options_including_passphrase()
    {
        string? stored = null;
        _services.SetPendingAutoBackupAsync(Arg.Any<Guid>(), Arg.Do<string?>(value => stored = value), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Queue().EnqueueAsync(Guid.NewGuid(), Options(), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stored);
        Assert.DoesNotContain("çok-gizli-parola-123", stored);
        var decrypted = JsonSerializer.Deserialize<ServiceBackupOptionsDto>(_protector.Unprotect(stored), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("çok-gizli-parola-123", decrypted!.EncryptionPassphrase);
        Assert.Equal("02:30", decrypted.ScheduleTime);
    }

    [Fact]
    public async Task Succeeded_install_creates_backup_job_with_stored_options()
    {
        var operation = Operation(ManagedServiceOperationStatus.Succeeded);
        var options = Options();
        _services.ClaimPendingAutoBackupAsync(operation.Id, Arg.Any<CancellationToken>())
            .Returns(_protector.Protect(JsonSerializer.Serialize(options, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        _backups.CreateJobAsync(operation.ServiceId, Arg.Any<ServiceBackupOptionsDto>(), Arg.Any<CancellationToken>()).Returns(ServiceResult<Guid>.Success(Guid.NewGuid()));

        var message = await Queue().ProcessAsync(operation.Id, Ct);

        Assert.Contains("oluşturuldu", message);
        await _backups.Received(1).CreateJobAsync(operation.ServiceId,
            Arg.Is<ServiceBackupOptionsDto>(o => o.StorageId == options.StorageId && o.EncryptionPassphrase == options.EncryptionPassphrase && o.KeepLast == 5),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ManagedServiceOperationStatus.Failed)]
    [InlineData(ManagedServiceOperationStatus.Interrupted)]
    public async Task Failed_or_interrupted_install_drops_request_without_job(ManagedServiceOperationStatus status)
    {
        var operation = Operation(status);
        _services.ClaimPendingAutoBackupAsync(operation.Id, Arg.Any<CancellationToken>()).Returns(_protector.Protect("{}"));

        var message = await Queue().ProcessAsync(operation.Id, Ct);

        Assert.Contains(ServiceAutoBackupQueue.FailedInstallMessage, message);
        await _backups.DidNotReceive().CreateJobAsync(Arg.Any<Guid>(), Arg.Any<ServiceBackupOptionsDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Running_operation_is_not_processed_and_request_is_kept()
    {
        var operation = Operation(ManagedServiceOperationStatus.Running);

        Assert.Null(await Queue().ProcessAsync(operation.Id, Ct));
        await _services.DidNotReceive().ClaimPendingAutoBackupAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Already_claimed_request_is_not_processed_twice()
    {
        var operation = Operation(ManagedServiceOperationStatus.Succeeded);
        _services.ClaimPendingAutoBackupAsync(operation.Id, Arg.Any<CancellationToken>()).Returns((string?)null);

        Assert.Null(await Queue().ProcessAsync(operation.Id, Ct));
        await _backups.DidNotReceive().CreateJobAsync(Arg.Any<Guid>(), Arg.Any<ServiceBackupOptionsDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Job_creation_failure_is_reported_as_warning()
    {
        var operation = Operation(ManagedServiceOperationStatus.Succeeded);
        _services.ClaimPendingAutoBackupAsync(operation.Id, Arg.Any<CancellationToken>())
            .Returns(_protector.Protect(JsonSerializer.Serialize(Options(), new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        _backups.CreateJobAsync(Arg.Any<Guid>(), Arg.Any<ServiceBackupOptionsDto>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<Guid>.Failure("Depolama bulunamadı."));

        var message = await Queue().ProcessAsync(operation.Id, Ct);

        Assert.Contains("Depolama bulunamadı.", message);
    }

    [Fact]
    public async Task Undecryptable_request_is_reported_and_dropped()
    {
        var operation = Operation(ManagedServiceOperationStatus.Succeeded);
        _services.ClaimPendingAutoBackupAsync(operation.Id, Arg.Any<CancellationToken>()).Returns("bozuk");

        var message = await Queue().ProcessAsync(operation.Id, Ct);

        Assert.Contains("okunamadı", message);
        await _backups.DidNotReceive().CreateJobAsync(Arg.Any<Guid>(), Arg.Any<ServiceBackupOptionsDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pending_list_carries_the_installing_user()
    {
        var operation = new ManagedServiceOperation { ServiceId = Guid.NewGuid(), UserId = "u1", UserName = "ayse", IpAddress = "10.0.0.1", Status = ManagedServiceOperationStatus.Succeeded };
        _services.ListPendingAutoBackupOperationsAsync(Arg.Any<CancellationToken>()).Returns([operation]);

        var pending = Assert.Single(await Queue().ListPendingAsync(Ct));

        Assert.Equal((operation.Id, operation.ServiceId, "u1", "ayse", "10.0.0.1"), (pending.OperationId, pending.ServiceId, pending.UserId, pending.UserName, pending.IpAddress));
    }
}
