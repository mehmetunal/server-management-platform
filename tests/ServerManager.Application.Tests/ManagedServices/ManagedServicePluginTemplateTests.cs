using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Docker;
using ServerManager.Application.Interfaces.ManagedServices;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.Plugins;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Validators.ManagedServices;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.ManagedServices;

/// <summary>Eklenti şablonuyla kurulan servisler: devre dışı eklenti, kancalar ve kurulum akışı.</summary>
public sealed class ManagedServicePluginTemplateTests
{
    private const string SystemName = "Test.Templates";
    private const string Key = "test.templates.app";

    private readonly IManagedServiceRepository _repository = Substitute.For<IManagedServiceRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IManagedServiceProvider _provider = Substitute.For<IManagedServiceProvider>();
    private readonly FakeSecretProtector _protector = new();
    private readonly ServiceActor _actor = new("u1", "admin@example.com", "127.0.0.1");
    private readonly Server _server = new() { Name = "web-1", IpAddress = "203.0.113.10" };
    private readonly PluginCatalog _plugins;
    private readonly RecordingHooks _hooks = new();
    private readonly ManagedServiceService _service;

    public ManagedServicePluginTemplateTests()
    {
        _plugins = new PluginCatalog([new LoadedPlugin(
            new PluginDescriptor { SystemName = SystemName, FriendlyName = "Test Şablonları" },
            Path.GetTempPath(),
            typeof(ManagedServicePluginTemplateTests).Assembly,
            null)]);
        _plugins.SetState(SystemName, true);
        var template = ServiceTemplateCatalogTests.Valid(Key, o => o.LogoFile = string.Empty);
        var catalog = new ServiceTemplateCatalog(_plugins, [new Provider(template)], [_hooks], NullLogger<ServiceTemplateCatalog>.Instance);

        _servers.FirstOrDefaultAsync(Arg.Any<Expression<Func<Server, bool>>>(), Arg.Any<CancellationToken>()).Returns(_server);
        _repository.ListOthersOnServerAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _connectionProvider.GetAsync(_server.Id, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection
            {
                ServerId = _server.Id,
                ServerName = _server.Name,
                Context = new RemoteExecutionContext { Connection = new SshConnectionRequest { Host = "203.0.113.10", Username = "root" } }
            }));

        var options = Options.Create(new ManagedServiceOptions());
        _service = new ManagedServiceService(
            _repository,
            _servers,
            _connectionProvider,
            _provider,
            Substitute.For<IDockerClient>(),
            _protector,
            Substitute.For<IAuditLogService>(),
            new CreateManagedServiceDtoValidator(options, catalog),
            new UpdateManagedServiceDtoValidator(options),
            new UpgradeManagedServiceDtoValidator(),
            options,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<ManagedServiceService>.Instance,
            catalog);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ManagedService Stored() => new()
    {
        ServerId = _server.Id,
        Server = _server,
        Name = "app",
        Slug = "app",
        TemplateKey = Key,
        ImageTag = "1.0",
        ContainerName = "sm-svc-app",
        EncryptedCredentials = _protector.Protect(new ServiceCredentials { Password = "Str0ngPassword123" }.ToJson()),
        PortBindings = ServicePortBindings.ToJson([new ServicePortBinding(8080, 18080)]),
        Status = ManagedServiceStatus.Running
    };

    private UpdateManagedServiceDto Settings() => new()
    {
        Ports = [new ServicePortFormItem { ContainerPort = 8080, Publish = true, HostPort = 18080 }]
    };

    [Fact]
    public async Task Disabled_plugin_blocks_recreate_and_upgrade_but_allows_listing_and_remove()
    {
        var stored = Stored();
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);
        _repository.ListAsync(null, Arg.Any<CancellationToken>()).Returns([stored]);
        _plugins.SetState(SystemName, false);

        var recreate = await _service.BeginRecreateAsync(stored.Id, Settings(), _actor, Ct);
        var upgrade = await _service.BeginUpgradeAsync(stored.Id, new UpgradeManagedServiceDto { ImageTag = "latest" }, _actor, Ct);
        var item = Assert.Single(await _service.ListAsync(null, Ct));
        var details = await _service.GetAsync(stored.Id, Ct);
        var remove = await _service.BeginRemoveAsync(stored.Id, new RemoveManagedServiceDto { ConfirmationName = "app" }, _actor, Ct);

        Assert.Equal(ServiceErrorType.Conflict, recreate.ErrorType);
        Assert.Contains("devre dışı", recreate.Message, StringComparison.Ordinal);
        Assert.Equal(ServiceErrorType.Conflict, upgrade.ErrorType);
        Assert.Equal(ServiceTemplateAvailability.PluginDisabled, item.TemplateAvailability);
        Assert.Equal("Şablon eklentisi devre dışı", item.TemplateBadge);
        Assert.False(item.CanChangeTemplateSettings);
        Assert.NotNull(item.Template);
        Assert.Equal(18080, Assert.Single(item.Ports).HostPort);
        Assert.True(details.IsSuccess);
        Assert.NotNull(details.Data!.TemplateBlockedMessage);
        Assert.True(remove.IsSuccess, remove.Message);
        Assert.Equal(Key, stored.TemplateKey);
    }

    [Fact]
    public async Task Disabled_plugin_template_cannot_be_installed()
    {
        _plugins.SetState(SystemName, false);

        var result = await _service.BeginInstallAsync(new CreateManagedServiceDto { ServerId = _server.Id, TemplateKey = Key, Name = "app", ImageTag = "1.0" }, _actor, Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateManagedServiceDto.TemplateKey));
    }

    [Fact]
    public async Task Recreate_runs_hook_validation_when_enabled()
    {
        var stored = Stored();
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);
        _hooks.Reject = true;

        var result = await _service.BeginRecreateAsync(stored.Id, Settings(), _actor, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Equal("Environment", Assert.Single(result.Errors).PropertyName);
        Assert.False(_hooks.LastForm!.IsInstall);
    }

    [Fact]
    public async Task Install_operation_appends_hook_arguments_and_runs_after_install()
    {
        var stored = Stored();
        var operation = new ManagedServiceOperation
        {
            ServiceId = stored.Id,
            Kind = ManagedServiceOperationKind.Install,
            Status = ManagedServiceOperationStatus.Running,
            ToTag = "1.0"
        };
        _repository.GetOperationAsync(operation.Id, Arg.Any<CancellationToken>()).Returns(operation);
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);
        ManagedServicePlan? plan = null;
        _provider.DeployAsync(Arg.Any<RemoteExecutionContext>(), Arg.Do<ManagedServicePlan>(p => plan = p), Arg.Any<IServiceOperationObserver>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServiceOperationResult>.Success(new ServiceOperationResult(true)));

        var result = await _service.RunOperationAsync(operation.Id, _actor, NullServiceOperationObserver.Instance, Ct);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(["--mode", "test"], plan!.Command);
        Assert.Equal("sm-svc-app", _hooks.Installed!.ContainerName);
        Assert.Equal(new ServiceEndpoint("sm-svc-app", 8080), _hooks.Installed.InternalEndpoint);
        Assert.Contains("hook ran", operation.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b[31", operation.Log.Replace("\u001b[31m==>", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hook_arguments_with_secrets_fail_the_operation()
    {
        var stored = Stored();
        var operation = new ManagedServiceOperation { ServiceId = stored.Id, Kind = ManagedServiceOperationKind.Recreate, Status = ManagedServiceOperationStatus.Running };
        _repository.GetOperationAsync(operation.Id, Arg.Any<CancellationToken>()).Returns(operation);
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);
        _hooks.LeakSecret = true;

        var result = await _service.RunOperationAsync(operation.Id, _actor, NullServiceOperationObserver.Instance, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("gizli", result.Message, StringComparison.Ordinal);
        await _provider.DidNotReceiveWithAnyArgs().DeployAsync(default!, default!, default!, Ct);
    }

    [Fact]
    public async Task Deploy_of_disabled_plugin_template_fails_with_reason()
    {
        var stored = Stored();
        var operation = new ManagedServiceOperation { ServiceId = stored.Id, Kind = ManagedServiceOperationKind.Recreate, Status = ManagedServiceOperationStatus.Running };
        _repository.GetOperationAsync(operation.Id, Arg.Any<CancellationToken>()).Returns(operation);
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);
        _plugins.SetState(SystemName, false);

        var result = await _service.RunOperationAsync(operation.Id, _actor, NullServiceOperationObserver.Instance, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("devre dışı", result.Message, StringComparison.Ordinal);
    }

    private sealed class Provider(ServiceTemplate template) : IServiceTemplateProvider
    {
        public IReadOnlyList<ServiceTemplate> GetTemplates() => [template];
    }

    private sealed class RecordingHooks : IServiceTemplateHooks
    {
        public bool Reject { get; set; }

        public bool LeakSecret { get; set; }

        public ServiceTemplateFormContext? LastForm { get; private set; }

        public ServiceTemplateInstallContext? Installed { get; private set; }

        public string TemplateKey => Key;

        public IEnumerable<ServiceTemplateHookError> Validate(ServiceTemplateFormContext context)
        {
            LastForm = context;
            return Reject ? [new ServiceTemplateHookError("Environment", "Eklenti reddetti.")] : [];
        }

        public IReadOnlyList<string> BuildExtraArgs(ServiceTemplateCommandContext context) =>
            LeakSecret ? ["--password=Str0ngPassword123"] : ["--mode", "test"];

        public async Task AfterInstallAsync(ServiceTemplateInstallContext context, IServiceTemplateHookLog log, CancellationToken cancellationToken)
        {
            Installed = context;
            await log.InfoAsync("hook ran \u001b[31m", cancellationToken);
        }
    }
}
