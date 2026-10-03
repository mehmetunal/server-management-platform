using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Dokploy;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Dokploy;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Dokploy;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Validators.Dokploy;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class DokployServiceTests
{
    private const string ServerName = "web-01";
    private const string BaseUrl = "http://203.0.113.10:3000";

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Server _server;
    private readonly IServerRepository _serverRepository = Substitute.For<IServerRepository>();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IDokployRepository _repository = Substitute.For<IDokployRepository>();
    private readonly IDokployProvider _provider = Substitute.For<IDokployProvider>();
    private readonly IDokployApiClient _apiClient = Substitute.For<IDokployApiClient>();
    private readonly FakeSecretProtector _protector = new();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDokployInstallObserver _observer = Substitute.For<IDokployInstallObserver>();
    private readonly RemoteExecutionContext _context = new() { Connection = new SshConnectionRequest { Host = "203.0.113.10", Username = "root" } };
    private readonly DokployActor _actor = new("user-1", "admin@example.com", "198.51.100.7");
    private readonly DokployService _service;

    public DokployServiceTests()
    {
        _server = new Server { Id = _serverId, Name = ServerName, IpAddress = "203.0.113.10" };
        _serverRepository.GetByIdAsync(_serverId, Arg.Any<CancellationToken>()).Returns(_server);
        _connectionProvider.GetAsync(_serverId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _serverId, ServerName = ServerName, Context = _context }));
        _provider.GatherFactsAsync(_context, Arg.Any<DokployOptions>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<DokployHostFacts>.Success(HealthyFacts()));
        _apiClient.ProbeHealthAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new DokployHttpProbeResult(true, 25, "ok"));
        _repository.GetInstallationsAsync(_serverId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _currentUser.UserName.Returns("admin@example.com");

        _service = new DokployService(
            _serverRepository,
            _connectionProvider,
            _repository,
            _provider,
            _apiClient,
            _protector,
            _auditLog,
            _currentUser,
            new DokploySettingsDtoValidator(),
            new DokployInstallRequestDtoValidator(),
            Options.Create(new DokployOptions { StartupTimeoutSeconds = 15 }),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<DokployService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DokployHostFacts HealthyFacts(string swarm = "inactive") => new()
    {
        OsId = "ubuntu",
        Kernel = "Linux",
        Architecture = "x86_64",
        UserId = 0,
        ContainerKind = "none",
        MemoryKb = 4L * 1024 * 1024,
        DiskAvailableKb = 80L * 1024 * 1024,
        CurlAvailable = true,
        BashAvailable = true,
        DockerInstalled = false,
        SwarmState = swarm,
        ListeningPorts = [22],
        ScriptReachable = true,
        RegistryReachable = true
    };

    private static DokployHostStatusDto InstalledHost() => new()
    {
        DockerAvailable = true,
        SwarmActive = true,
        Services = [new DokployServiceDto { Name = "dokploy", Image = "dokploy/dokploy:v0.25.3", RunningReplicas = 1, DesiredReplicas = 1 }],
        Containers = [new DokployContainerDto { Name = "dokploy-traefik", State = "running" }],
        ImageTag = "v0.25.3",
        LocalHealthy = true
    };

    private DokployInstallation RunningInstallation() => new()
    {
        ServerId = _serverId,
        ServerName = ServerName,
        ScriptUrl = "https://dokploy.com/install.sh",
        Status = DokployInstallationStatus.Running
    };

    private Task AssertAuditedAsync(string action, bool isSuccess) =>
        _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == action && e.IsSuccess == isSuccess && e.EntityId == _serverId.ToString() && e.TargetName == ServerName),
            Arg.Any<CancellationToken>());

    [Theory]
    [InlineData("Web-01")]
    [InlineData("other")]
    public async Task Begin_requires_exact_server_name(string confirmation)
    {
        var result = await _service.BeginInstallationAsync(_serverId, new DokployInstallRequestDto { ConfirmationName = confirmation }, _actor, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceiveWithAnyArgs().AddInstallationAsync(default!, Ct);
    }

    [Fact]
    public async Task Begin_rejects_when_installation_is_running()
    {
        _repository.GetRunningInstallationAsync(_serverId, Arg.Any<CancellationToken>()).Returns(RunningInstallation());

        var result = await _service.BeginInstallationAsync(_serverId, new DokployInstallRequestDto { ConfirmationName = ServerName }, _actor, Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        await _provider.DidNotReceiveWithAnyArgs().GatherFactsAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task Begin_rejects_incompatible_server()
    {
        _provider.GatherFactsAsync(_context, Arg.Any<DokployOptions>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<DokployHostFacts>.Success(HealthyFacts(swarm: "active")));

        var result = await _service.BeginInstallationAsync(_serverId, new DokployInstallRequestDto { ConfirmationName = ServerName }, _actor, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains("Docker Swarm", result.Message);
        await _repository.DidNotReceiveWithAnyArgs().AddInstallationAsync(default!, Ct);
    }

    [Fact]
    public async Task Begin_records_running_installation_and_audits_actor()
    {
        var result = await _service.BeginInstallationAsync(_serverId, new DokployInstallRequestDto { ConfirmationName = $" {ServerName} ", Version = "v0.25.3" }, _actor, Ct);

        Assert.True(result.IsSuccess);
        await _repository.Received(1).AddInstallationAsync(
            Arg.Is<DokployInstallation>(i => i.Id == result.Data
                                             && i.Status == DokployInstallationStatus.Running
                                             && i.RequestedVersion == "v0.25.3"
                                             && i.UserName == _actor.UserName
                                             && i.IpAddress == _actor.IpAddress),
            Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.DokployInstallStart && e.UserNameOverride == _actor.UserName && e.Details!.Contains("v0.25.3")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Successful_run_creates_instance_and_completes_record()
    {
        var installation = RunningInstallation();
        _repository.GetInstallationAsync(installation.Id, Arg.Any<CancellationToken>()).Returns(installation);
        _provider.RunInstallScriptAsync(_context, Arg.Any<DokployInstallPlan>(), Arg.Any<IDokployInstallObserver>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<DokployScriptResult>.Success(new DokployScriptResult { ExitCode = 0, Sha256 = new string('a', 64) }));
        _provider.IsLocallyHealthyAsync(_context, 3000, Arg.Any<CancellationToken>()).Returns(true);
        _provider.GetStatusAsync(_context, 3000, Arg.Any<CancellationToken>()).Returns(ServiceResult<DokployHostStatusDto>.Success(InstalledHost()));

        var result = await _service.RunInstallationAsync(installation.Id, _actor, _observer, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(DokployInstallationStatus.Succeeded, installation.Status);
        Assert.Equal(new string('a', 64), installation.ScriptSha256);
        Assert.Contains("Dokploy kuruldu", installation.Output);
        await _provider.Received(1).RunInstallScriptAsync(_context, Arg.Is<DokployInstallPlan>(p => !p.Elevate && p.UseBash), Arg.Any<IDokployInstallObserver>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(
            Arg.Is<DokployInstance>(i => i.BaseUrl == BaseUrl && i.InstallationId == installation.Id && i.Status == DokployStatus.Running && i.Version == "v0.25.3"),
            Arg.Any<CancellationToken>());
        await _observer.Received(1).OnStageAsync(DokployInstallStage.Completed, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.DokployInstallComplete && e.IsSuccess && e.UserIdOverride == _actor.UserId && e.IpAddressOverride == _actor.IpAddress),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_script_marks_installation_failed()
    {
        var installation = RunningInstallation();
        _repository.GetInstallationAsync(installation.Id, Arg.Any<CancellationToken>()).Returns(installation);
        _provider.RunInstallScriptAsync(_context, Arg.Any<DokployInstallPlan>(), Arg.Any<IDokployInstallObserver>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<DokployScriptResult>.Success(new DokployScriptResult { ExitCode = 1 }));

        var result = await _service.RunInstallationAsync(installation.Id, _actor, _observer, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(DokployInstallationStatus.Failed, installation.Status);
        Assert.Equal(1, installation.ExitCode);
        Assert.Contains("çıkış kodu 1", installation.FailureReason);
        await _provider.DidNotReceiveWithAnyArgs().IsLocallyHealthyAsync(default!, default, Ct);
        await _observer.Received(1).OnStageAsync(DokployInstallStage.Failed, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await AssertAuditedAsync(AuditActions.DokployInstallComplete, false);
    }

    [Fact]
    public async Task Cancelled_run_marks_installation_interrupted()
    {
        var installation = RunningInstallation();
        _repository.GetInstallationAsync(installation.Id, Arg.Any<CancellationToken>()).Returns(installation);
        using var cancellation = new CancellationTokenSource();
        _provider.RunInstallScriptAsync(_context, Arg.Any<DokployInstallPlan>(), Arg.Any<IDokployInstallObserver>(), Arg.Any<CancellationToken>())
            .Returns<Task<ServiceResult<DokployScriptResult>>>(_ =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });

        var result = await _service.RunInstallationAsync(installation.Id, _actor, _observer, cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(DokployInstallationStatus.Interrupted, installation.Status);
        Assert.NotNull(installation.CompletedAt);
    }

    [Fact]
    public async Task Overview_registers_externally_installed_dokploy()
    {
        _provider.GetStatusAsync(_context, 3000, Arg.Any<CancellationToken>()).Returns(ServiceResult<DokployHostStatusDto>.Success(InstalledHost()));

        var result = await _service.GetOverviewAsync(_serverId, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data!.Instance);
        Assert.Equal(DokployStatus.Running, result.Data.Instance!.Status);
        Assert.False(result.Data.Instance.InstalledByPanel);
        await _repository.Received(1).AddAsync(Arg.Is<DokployInstance>(i => i.BaseUrl == BaseUrl), Arg.Any<CancellationToken>());
        await AssertAuditedAsync(AuditActions.DokployDetected, true);
    }

    [Fact]
    public async Task Overview_without_dokploy_does_not_create_instance()
    {
        _provider.GetStatusAsync(_context, 3000, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<DokployHostStatusDto>.Success(new DokployHostStatusDto { DockerAvailable = true }));

        var result = await _service.GetOverviewAsync(_serverId, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Instance);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task Rejected_api_key_is_reported_on_api_key_field()
    {
        _apiClient.GetVersionAsync(BaseUrl, "bad-key", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<string>.Failure("API anahtarı geçersiz veya yetkisiz.", ServiceErrorType.Forbidden));

        var result = await _service.SaveSettingsAsync(_serverId, new DokploySettingsDto { BaseUrl = BaseUrl + "/", ApiKey = "bad-key" }, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DokploySettingsDto.ApiKey));
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Verified_api_key_is_stored_encrypted()
    {
        var instance = new DokployInstance { ServerId = _serverId, BaseUrl = BaseUrl };
        _repository.GetByServerIdAsync(_serverId, Arg.Any<CancellationToken>()).Returns(instance);
        _apiClient.GetVersionAsync(BaseUrl, "good-key", Arg.Any<CancellationToken>()).Returns(ServiceResult<string>.Success("v0.25.3"));

        var result = await _service.SaveSettingsAsync(_serverId, new DokploySettingsDto { BaseUrl = BaseUrl, ApiKey = "good-key" }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(_protector.Protect("good-key"), instance.EncryptedApiKey);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.DokploySettingsUpdate && !e.Details!.Contains("good-key")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Projects_require_api_key()
    {
        _repository.GetByServerIdAsync(_serverId, Arg.Any<CancellationToken>()).Returns(new DokployInstance { ServerId = _serverId, BaseUrl = BaseUrl });

        var result = await _service.GetProjectsAsync(_serverId, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _apiClient.DidNotReceiveWithAnyArgs().GetProjectsAsync(default!, default!, Ct);
    }
}
