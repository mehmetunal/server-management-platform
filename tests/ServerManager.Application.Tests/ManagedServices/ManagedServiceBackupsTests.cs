using NSubstitute;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.Services;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.ManagedServices;

public class ManagedServiceBackupsTests
{
    private readonly IManagedServiceService _services = Substitute.For<IManagedServiceService>();
    private readonly IManagedServiceRepository _serviceRepository = Substitute.For<IManagedServiceRepository>();
    private readonly IBackupJobService _jobs = Substitute.For<IBackupJobService>();
    private readonly IBackupRepository _backups = Substitute.For<IBackupRepository>();
    private readonly ManagedServiceBackupService _service;
    private readonly Guid _storageId = Guid.NewGuid();

    public ManagedServiceBackupsTests()
    {
        _backups.GetStorageAsync(_storageId, Arg.Any<CancellationToken>()).Returns(new BackupStorage { Name = "s3" });
        _jobs.CreateForContainerDatabaseAsync(Arg.Any<ContainerDatabaseBackupRequest>(), Arg.Any<CancellationToken>()).Returns(ServiceResult<Guid>.Success(Guid.NewGuid()));
        _service = new ManagedServiceBackupService(_services, _serviceRepository, _jobs, _backups);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("postgres", BackupDatabaseEngine.PostgreSql)]
    [InlineData("mysql", BackupDatabaseEngine.MySql)]
    [InlineData("mariadb", BackupDatabaseEngine.MySql)]
    [InlineData("mongodb", BackupDatabaseEngine.MongoDb)]
    [InlineData("redis", BackupDatabaseEngine.Redis)]
    [InlineData("mssql", BackupDatabaseEngine.SqlServer)]
    public void Database_templates_map_to_backup_engines(string template, BackupDatabaseEngine engine) =>
        Assert.Equal(engine, ManagedServiceBackups.EngineFor(template));

    [Theory]
    [InlineData("minio")]
    [InlineData("n8n")]
    [InlineData(null)]
    public void Application_templates_have_no_backup_engine(string? template) =>
        Assert.False(ManagedServiceBackups.Supports(template));

    private void Service(string template, string? username, string? database)
    {
        var info = new ManagedServiceConnectionInfo
        {
            ServiceId = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            Name = "db",
            TemplateKey = template,
            Host = "sm-svc-db",
            Username = username,
            Password = "S3cret-Pa55word",
            Database = database
        };
        _services.GetConnectionInfoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ServiceResult<ManagedServiceConnectionInfo>.Success(info));
    }

    [Theory]
    [InlineData("postgres", "app", "app", "app", "app")]
    [InlineData("mysql", "app", "app", "root", "app")]
    [InlineData("mongodb", "admin", "app", "admin", null)]
    [InlineData("redis", null, null, null, null)]
    public async Task Job_uses_service_container_and_credentials(string template, string? username, string? database, string? expectedUser, string? expectedDatabase)
    {
        Service(template, username, database);

        var result = await _service.CreateJobAsync(Guid.NewGuid(), new ServiceBackupOptionsDto { StorageId = _storageId, KeepLast = 5, ScheduleTime = "02:30" }, Ct);

        Assert.True(result.IsSuccess, result.Message);
        await _jobs.Received(1).CreateForContainerDatabaseAsync(
            Arg.Is<ContainerDatabaseBackupRequest>(r =>
                r.ContainerName == "sm-svc-db"
                && r.StorageId == _storageId
                && r.User == expectedUser
                && r.DatabaseName == expectedDatabase
                && r.Password == "S3cret-Pa55word"
                && r.KeepLast == 5
                && r.ScheduleTime == "02:30"
                && r.ScheduleType == BackupScheduleType.Daily),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sql_server_requires_a_database_name_and_storage()
    {
        Service("mssql", "sa", null);

        var missingDatabase = await _service.CreateJobAsync(Guid.NewGuid(), new ServiceBackupOptionsDto { StorageId = _storageId }, Ct);
        var missingStorage = await _service.CreateJobAsync(Guid.NewGuid(), new ServiceBackupOptionsDto { DatabaseName = "app" }, Ct);
        var ok = await _service.CreateJobAsync(Guid.NewGuid(), new ServiceBackupOptionsDto { StorageId = _storageId, DatabaseName = "app" }, Ct);

        Assert.Equal(ServiceErrorType.Validation, missingDatabase.ErrorType);
        Assert.Equal(ServiceErrorType.Validation, missingStorage.ErrorType);
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public async Task Application_template_and_short_passphrase_are_rejected()
    {
        var app = await _service.ValidateOptionsAsync("minio", null, new ServiceBackupOptionsDto { StorageId = _storageId }, Ct);
        var passphrase = await _service.ValidateOptionsAsync("postgres", "app", new ServiceBackupOptionsDto { StorageId = _storageId, EncryptionPassphrase = "short" }, Ct);

        Assert.False(app.IsSuccess);
        Assert.False(passphrase.IsSuccess);
    }

    [Fact]
    public async Task Jobs_are_matched_by_server_and_container()
    {
        var service = new ManagedService { ServerId = Guid.NewGuid(), ContainerName = "sm-svc-db" };
        _serviceRepository.GetAsync(service.Id, Arg.Any<CancellationToken>()).Returns(service);
        _jobs.GetJobsAsync(service.ServerId, Arg.Any<CancellationToken>()).Returns([
            new BackupJobListItemDto { Name = "match", SourceType = BackupSourceType.Database, ContainerName = "sm-svc-db" },
            new BackupJobListItemDto { Name = "other", SourceType = BackupSourceType.Database, ContainerName = "sm-svc-other" },
            new BackupJobListItemDto { Name = "files", SourceType = BackupSourceType.Files }
        ]);

        var jobs = await _service.ListJobsAsync(service.Id, Ct);

        Assert.Equal("match", Assert.Single(jobs).Name);
    }
}
