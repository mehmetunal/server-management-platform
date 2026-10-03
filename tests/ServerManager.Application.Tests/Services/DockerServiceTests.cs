using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Docker;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Services;
using ServerManager.Application.Validators.Docker;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class DockerServiceTests
{
    private const string ServerName = "web-01";

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IDockerClient _dockerClient = Substitute.For<IDockerClient>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly RemoteExecutionContext _context = new() { Connection = new SshConnectionRequest { Host = "10.0.0.5", Username = "deploy" } };
    private readonly DockerService _service;

    public DockerServiceTests()
    {
        _connectionProvider.GetAsync(_serverId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _serverId, ServerName = ServerName, Context = _context }));

        _service = new DockerService(
            _connectionProvider,
            _dockerClient,
            _auditLog,
            new RenameContainerDtoValidator(),
            new PullImageDtoValidator(),
            new CreateVolumeDtoValidator(),
            new CreateNetworkDtoValidator(),
            Options.Create(new DockerOptions { DefaultLogTail = 200, MaxLogTail = 5000 }),
            NullLogger<DockerService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task AssertAuditedAsync(string action, bool isSuccess, string detailsFragment) =>
        _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == action
                                    && e.IsSuccess == isSuccess
                                    && e.EntityId == _serverId.ToString()
                                    && e.TargetName == ServerName
                                    && e.Details != null && e.Details.Contains(detailsFragment)),
            Arg.Any<CancellationToken>());

    [Fact]
    public async Task Container_action_runs_client_and_writes_audit()
    {
        _dockerClient.ExecuteContainerActionAsync(_context, "web", DockerContainerAction.Restart, false, Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Success());

        var result = await _service.ExecuteContainerActionAsync(_serverId, new ContainerActionRequest { Container = "web", Action = DockerContainerAction.Restart }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("web yeniden başlatıldı.", result.Message);
        await AssertAuditedAsync(AuditActions.DockerContainerRestart, true, "Container: web");
    }

    [Fact]
    public async Task Failed_container_action_is_audited_with_error()
    {
        _dockerClient.ExecuteContainerActionAsync(_context, "web", DockerContainerAction.Stop, false, Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Failure("Container çalışmıyor."));

        var result = await _service.ExecuteContainerActionAsync(_serverId, new ContainerActionRequest { Container = "web", Action = DockerContainerAction.Stop }, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("Container çalışmıyor.", result.Message);
        await AssertAuditedAsync(AuditActions.DockerContainerStop, false, "Hata: Container çalışmıyor.");
    }

    [Fact]
    public async Task Force_flag_is_ignored_for_non_remove_actions()
    {
        _dockerClient.ExecuteContainerActionAsync(_context, "web", DockerContainerAction.Stop, false, Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Success());

        await _service.ExecuteContainerActionAsync(_serverId, new ContainerActionRequest { Container = "web", Action = DockerContainerAction.Stop, Force = true }, Ct);

        await _dockerClient.Received(1).ExecuteContainerActionAsync(_context, "web", DockerContainerAction.Stop, false, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Web")]
    [InlineData("other")]
    public async Task Remove_requires_exact_container_name_confirmation(string? confirmation)
    {
        var request = new ContainerActionRequest { Container = "web", Action = DockerContainerAction.Remove, ConfirmationName = confirmation };

        var result = await _service.ExecuteContainerActionAsync(_serverId, request, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _dockerClient.DidNotReceiveWithAnyArgs().ExecuteContainerActionAsync(default!, default!, default, default, Ct);
        await _auditLog.DidNotReceiveWithAnyArgs().LogAsync(default!, Ct);
    }

    [Fact]
    public async Task Confirmed_force_remove_passes_force_and_marks_audit()
    {
        _dockerClient.ExecuteContainerActionAsync(_context, "web", DockerContainerAction.Remove, true, Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Success());

        var request = new ContainerActionRequest { Container = "web", Action = DockerContainerAction.Remove, Force = true, ConfirmationName = " web " };
        var result = await _service.ExecuteContainerActionAsync(_serverId, request, Ct);

        Assert.True(result.IsSuccess);
        await AssertAuditedAsync(AuditActions.DockerContainerRemove, true, "(zorla)");
    }

    [Theory]
    [InlineData("web;reboot")]
    [InlineData("")]
    public async Task Invalid_container_name_never_reaches_server(string container)
    {
        var result = await _service.ExecuteContainerActionAsync(_serverId, new ContainerActionRequest { Container = container, Action = DockerContainerAction.Start }, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _connectionProvider.DidNotReceiveWithAnyArgs().GetAsync(default, Ct);
    }

    [Fact]
    public async Task Undefined_action_is_rejected()
    {
        var result = await _service.ExecuteContainerActionAsync(_serverId, new ContainerActionRequest { Container = "web", Action = (DockerContainerAction)42 }, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Connection_failure_is_returned_without_audit()
    {
        var otherServer = Guid.NewGuid();
        _connectionProvider.GetAsync(otherServer, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Failure("Host key doğrulanmamış.", ServiceErrorType.Conflict));

        var result = await _service.ExecuteContainerActionAsync(otherServer, new ContainerActionRequest { Container = "web", Action = DockerContainerAction.Start }, Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        await _auditLog.DidNotReceiveWithAnyArgs().LogAsync(default!, Ct);
    }

    [Theory]
    [InlineData(null, 200)]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(100000, 5000)]
    public async Task Log_tail_is_clamped(int? requested, int expected)
    {
        _dockerClient.GetContainerLogsAsync(_context, "web", Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<DockerLogsDto>.Success(new DockerLogsDto { Container = "web" }));

        await _service.GetContainerLogsAsync(_serverId, new DockerLogQuery { Container = "web", Tail = requested }, Ct);

        await _dockerClient.Received(1).GetContainerLogsAsync(_context, "web", expected, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_log_since_is_rejected()
    {
        var result = await _service.GetContainerLogsAsync(_serverId, new DockerLogQuery { Container = "web", Since = "yesterday; id" }, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _dockerClient.DidNotReceiveWithAnyArgs().GetContainerLogsAsync(default!, default!, default, default, Ct);
    }

    [Fact]
    public async Task Valid_log_since_is_passed_through()
    {
        const string since = "2026-10-03T18:48:19.219917720Z";
        _dockerClient.GetContainerLogsAsync(_context, "web", Arg.Any<int>(), since, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<DockerLogsDto>.Success(new DockerLogsDto { Container = "web" }));

        var result = await _service.GetContainerLogsAsync(_serverId, new DockerLogQuery { Container = "web", Since = since, Tail = 5000 }, Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Prune_requires_server_name_confirmation()
    {
        var result = await _service.PruneImagesAsync(_serverId, all: true, confirmationName: "wrong", Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _dockerClient.DidNotReceiveWithAnyArgs().PruneImagesAsync(default!, default, Ct);
    }

    [Fact]
    public async Task Confirmed_prune_is_audited()
    {
        _dockerClient.PruneImagesAsync(_context, true, Arg.Any<CancellationToken>()).Returns(ServiceResult.Success("12 MB boşaltıldı."));

        var result = await _service.PruneImagesAsync(_serverId, all: true, confirmationName: ServerName, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("12 MB boşaltıldı.", result.Message);
        await AssertAuditedAsync(AuditActions.DockerImagePrune, true, "Kullanılmayan tüm image'lar");
    }

    [Fact]
    public async Task Volume_remove_requires_volume_name_confirmation()
    {
        var result = await _service.RemoveVolumeAsync(_serverId, "webdata", "web", Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _dockerClient.DidNotReceiveWithAnyArgs().RemoveVolumeAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task Confirmed_volume_remove_is_audited()
    {
        _dockerClient.RemoveVolumeAsync(_context, "webdata", Arg.Any<CancellationToken>()).Returns(ServiceResult.Success());

        var result = await _service.RemoveVolumeAsync(_serverId, "webdata", "webdata", Ct);

        Assert.True(result.IsSuccess);
        await AssertAuditedAsync(AuditActions.DockerVolumeRemove, true, "Volume: webdata");
    }

    [Theory]
    [InlineData("bridge")]
    [InlineData("host")]
    [InlineData("none")]
    public async Task System_networks_cannot_be_removed(string network)
    {
        var result = await _service.RemoveNetworkAsync(_serverId, network, Ct);

        Assert.False(result.IsSuccess);
        await _dockerClient.DidNotReceiveWithAnyArgs().RemoveNetworkAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task Pull_trims_reference_and_validates()
    {
        _dockerClient.PullImageAsync(_context, "nginx:alpine", Arg.Any<CancellationToken>()).Returns(ServiceResult.Success());

        var ok = await _service.PullImageAsync(_serverId, new PullImageDto { Reference = "  nginx:alpine " }, Ct);
        var invalid = await _service.PullImageAsync(_serverId, new PullImageDto { Reference = "nginx; reboot" }, Ct);

        Assert.True(ok.IsSuccess);
        Assert.Equal(ServiceErrorType.Validation, invalid.ErrorType);
        await _dockerClient.Received(1).PullImageAsync(Arg.Any<RemoteExecutionContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await AssertAuditedAsync(AuditActions.DockerImagePull, true, "Image: nginx:alpine");
    }

    [Fact]
    public async Task Create_network_normalizes_empty_optional_fields()
    {
        _dockerClient.CreateNetworkAsync(_context, Arg.Any<CreateNetworkDto>(), Arg.Any<CancellationToken>()).Returns(ServiceResult.Success());

        var result = await _service.CreateNetworkAsync(_serverId, new CreateNetworkDto { Name = " appnet ", Subnet = " ", Gateway = "" }, Ct);

        Assert.True(result.IsSuccess);
        await _dockerClient.Received(1).CreateNetworkAsync(
            _context,
            Arg.Is<CreateNetworkDto>(d => d.Name == "appnet" && d.Subnet == null && d.Gateway == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Terminal_open_is_audited_and_returns_handle()
    {
        var session = Substitute.For<ITerminalSession>();
        var sink = Substitute.For<ITerminalOutputSink>();
        _dockerClient.OpenContainerTerminalAsync(_context, "web", 500, 5, sink, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ITerminalSession>.Success(session));

        var result = await _service.OpenTerminalAsync(_serverId, "web", 9999, 1, sink, Ct);

        Assert.True(result.IsSuccess);
        Assert.Same(session, result.Data!.Session);
        Assert.Equal(ServerName, result.Data.ServerName);
        Assert.Equal(TerminalSessionKind.Container, result.Data.Kind);
        await AssertAuditedAsync(AuditActions.DockerTerminalOpen, true, "Container: web");
    }
}
