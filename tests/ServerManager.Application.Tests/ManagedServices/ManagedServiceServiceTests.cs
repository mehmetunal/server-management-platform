using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Docker;
using ServerManager.Application.Interfaces.ManagedServices;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Validators.ManagedServices;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.ManagedServices;

public class ManagedServiceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly IManagedServiceRepository _repository = Substitute.For<IManagedServiceRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IManagedServiceProvider _provider = Substitute.For<IManagedServiceProvider>();
    private readonly IDockerClient _docker = Substitute.For<IDockerClient>();
    private readonly FakeSecretProtector _protector = new();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly RemoteExecutionContext _context = new() { Connection = new SshConnectionRequest { Host = "203.0.113.10", Username = "root" } };
    private readonly ServiceActor _actor = new("u1", "admin@example.com", "127.0.0.1");
    private readonly Server _server = new() { Name = "web-1", IpAddress = "203.0.113.10" };
    private readonly ManagedServiceService _service;
    private readonly List<ManagedService> _added = [];
    private readonly List<ManagedServiceOperation> _operations = [];

    public ManagedServiceServiceTests()
    {
        _servers.FirstOrDefaultAsync(Arg.Any<Expression<Func<Server, bool>>>(), Arg.Any<CancellationToken>()).Returns(_server);
        _repository.ListOthersOnServerAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _repository.When(r => r.AddAsync(Arg.Any<ManagedService>(), Arg.Any<CancellationToken>())).Do(c => _added.Add(c.Arg<ManagedService>()));
        _repository.When(r => r.AddOperationAsync(Arg.Any<ManagedServiceOperation>(), Arg.Any<CancellationToken>())).Do(c => _operations.Add(c.Arg<ManagedServiceOperation>()));
        _connectionProvider.GetAsync(_server.Id, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _server.Id, ServerName = _server.Name, Context = _context }));

        var options = Options.Create(new ManagedServiceOptions());
        _service = new ManagedServiceService(
            _repository,
            _servers,
            _connectionProvider,
            _provider,
            _docker,
            _protector,
            _audit,
            new CreateManagedServiceDtoValidator(options),
            new UpdateManagedServiceDtoValidator(options),
            new UpgradeManagedServiceDtoValidator(),
            options,
            new FixedTimeProvider(Now),
            NullLogger<ManagedServiceService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CreateManagedServiceDto Dto(string template = ServiceTemplates.Postgres, string? password = null) => new()
    {
        ServerId = _server.Id,
        TemplateKey = template,
        Name = "Ana DB",
        ImageTag = "17",
        Username = "app",
        Password = password,
        Database = "app",
        Ports = [new ServicePortFormItem { ContainerPort = 5432, Publish = true, HostPort = 15432 }],
        ExposePublicly = true,
        AllowedSourceIps = "203.0.113.20, 10.0.0.9/24"
    };

    private ManagedService Stored(string template = ServiceTemplates.Postgres, string tag = "17")
    {
        var credentials = new ServiceCredentials { Username = "app", Password = "Str0ngPassword123", Database = "app" };
        return new ManagedService
        {
            ServerId = _server.Id,
            Server = _server,
            Name = "Ana DB",
            Slug = "ana-db",
            TemplateKey = template,
            ImageTag = tag,
            ContainerName = "sm-svc-ana-db",
            EncryptedCredentials = _protector.Protect(credentials.ToJson()),
            PortBindings = ServicePortBindings.ToJson([new ServicePortBinding(5432, 15432)]),
            ExposePublicly = true,
            AllowedSourceIps = "203.0.113.20/32",
            JoinProxyNetwork = true,
            Networks = "app_default",
            Status = ManagedServiceStatus.Running
        };
    }

    [Fact]
    public async Task Install_generates_password_encrypts_credentials_and_records_operation()
    {
        var result = await _service.BeginInstallAsync(Dto(), _actor, Ct);

        Assert.True(result.IsSuccess, result.Message);
        var service = Assert.Single(_added);
        var operation = Assert.Single(_operations);
        Assert.Equal("ana-db", service.Slug);
        Assert.Equal("sm-svc-ana-db", service.ContainerName);
        Assert.Equal(ManagedServiceStatus.Installing, service.Status);
        Assert.Equal(ManagedServiceOperationKind.Install, operation.Kind);
        Assert.Equal(service.Id, result.Data!.ServiceId);
        Assert.Equal(operation.Id, result.Data.OperationId);

        var credentials = ServiceCredentials.FromJson(_protector.Unprotect(service.EncryptedCredentials));
        Assert.Equal(ServiceSecrets.DefaultPasswordLength, credentials.Password!.Length);
        Assert.DoesNotContain(credentials.Password, service.EncryptedCredentials);
        Assert.Equal("203.0.113.20/32\n10.0.0.0/24", service.AllowedSourceIps);

        await _audit.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ManagedServiceCreate && e.EntityType == AuditEntityTypes.ManagedService
                                    && !e.Details!.Contains(credentials.Password)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Install_uses_unique_slug_per_server()
    {
        _repository.SlugExistsAsync(_server.Id, "ana-db", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.BeginInstallAsync(Dto(password: "Str0ngPassword123"), _actor, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("ana-db-2", _added[0].Slug);
    }

    [Fact]
    public async Task Install_rejects_ports_used_by_other_services()
    {
        var other = Stored();
        other.Name = "Diğer";
        _repository.ListOthersOnServerAsync(_server.Id, Guid.Empty, Arg.Any<CancellationToken>()).Returns([other]);

        var result = await _service.BeginInstallAsync(Dto(), _actor, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("Ports[0].HostPort", result.Errors.Single().PropertyName);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task Install_rejects_duplicate_name_on_server()
    {
        _repository.NameExistsAsync(_server.Id, "Ana DB", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.BeginInstallAsync(Dto(), _actor, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("Name", result.Errors.Single().PropertyName);
    }

    [Fact]
    public async Task N8n_install_generates_encryption_key_without_password()
    {
        var dto = new CreateManagedServiceDto
        {
            ServerId = _server.Id,
            TemplateKey = ServiceTemplates.N8n,
            Name = "Otomasyon",
            ImageTag = "stable",
            Ports = [new ServicePortFormItem { ContainerPort = 5678, Publish = true, HostPort = 15678 }]
        };

        var result = await _service.BeginInstallAsync(dto, _actor, Ct);

        Assert.True(result.IsSuccess, result.Message);
        var credentials = ServiceCredentials.FromJson(_protector.Unprotect(_added[0].EncryptedCredentials));
        Assert.Null(credentials.Password);
        Assert.Matches("^[0-9a-f]{64}$", credentials.EncryptionKey!);
    }

    [Fact]
    public async Task Connection_info_uses_internal_network_address()
    {
        var stored = Stored();
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);

        var result = await _service.GetConnectionInfoAsync(stored.Id, Ct);

        Assert.True(result.IsSuccess);
        var info = result.Data!;
        Assert.Equal("sm-svc-ana-db", info.Host);
        Assert.Equal(5432, info.Port);
        Assert.Equal("Str0ngPassword123", info.Password);
        Assert.Equal("postgres://app:Str0ngPassword123@sm-svc-ana-db:5432/app", info.ConnectionString);
        Assert.Equal(info.ConnectionString, info.SuggestedEnvironment["DATABASE_URL"]);
        Assert.Equal(["sm-services", "sm-proxy", "app_default"], info.Networks);
        Assert.DoesNotContain("Str0ngPassword123", info.ToString());
    }

    [Fact]
    public async Task Details_mask_password_in_connection_previews()
    {
        var stored = Stored();
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);

        var result = await _service.GetAsync(stored.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("postgres://app:****@sm-svc-ana-db:5432/app", result.Data!.InternalConnectionPreview);
        Assert.Equal("postgres://app:****@203.0.113.10:15432/app", result.Data.ExternalConnectionPreview);
        Assert.Equal(["203.0.113.20/32"], result.Data.AllowedSources);
    }

    [Fact]
    public async Task Reveal_is_audited()
    {
        var stored = Stored();
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);

        var result = await _service.RevealSecretsAsync(stored.Id, Ct);

        Assert.Equal("Str0ngPassword123", result.Data!.Password);
        await _audit.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.ManagedServiceRevealSecrets && e.IsSuccess), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Major_upgrade_requires_confirmation()
    {
        var stored = Stored();
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);

        var rejected = await _service.BeginUpgradeAsync(stored.Id, new UpgradeManagedServiceDto { ImageTag = "18" }, _actor, Ct);
        Assert.False(rejected.IsSuccess);
        Assert.Equal(nameof(UpgradeManagedServiceDto.ConfirmMajorUpgrade), rejected.Errors.Single().PropertyName);

        var accepted = await _service.BeginUpgradeAsync(stored.Id, new UpgradeManagedServiceDto { ImageTag = "18", ConfirmMajorUpgrade = true }, _actor, Ct);
        Assert.True(accepted.IsSuccess);
        var operation = Assert.Single(_operations);
        Assert.Equal(("17", "18"), (operation.FromTag, operation.ToTag));
        Assert.Equal("17", stored.ImageTag);
    }

    [Fact]
    public void Major_upgrade_warning_rules()
    {
        var postgres = ServiceTemplates.Find(ServiceTemplates.Postgres)!;
        var redis = ServiceTemplates.Find(ServiceTemplates.Redis)!;

        Assert.Null(ManagedServiceService.MajorUpgradeWarning(postgres, "17", "17"));
        Assert.Null(ManagedServiceService.MajorUpgradeWarning(postgres, "16.3", "16.4"));
        Assert.Contains("yükseltmesidir", ManagedServiceService.MajorUpgradeWarning(postgres, "16", "17"));
        Assert.Contains("düşürmesidir", ManagedServiceService.MajorUpgradeWarning(postgres, "17", "16"));
        Assert.NotNull(ManagedServiceService.MajorUpgradeWarning(postgres, "17", "latest"));
        Assert.Null(ManagedServiceService.MajorUpgradeWarning(redis, "7.4", "8"));
    }

    [Fact]
    public async Task Remove_requires_exact_name()
    {
        var stored = Stored();
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);

        var rejected = await _service.BeginRemoveAsync(stored.Id, new RemoveManagedServiceDto { ConfirmationName = "ana db" }, _actor, Ct);
        Assert.False(rejected.IsSuccess);

        var accepted = await _service.BeginRemoveAsync(stored.Id, new RemoveManagedServiceDto { ConfirmationName = "Ana DB", RemoveData = true }, _actor, Ct);
        Assert.True(accepted.IsSuccess);
        Assert.True(Assert.Single(_operations).RemoveData);
        Assert.Equal(ManagedServiceStatus.Removing, stored.Status);
    }

    [Fact]
    public async Task Busy_service_rejects_new_operations()
    {
        var stored = Stored();
        _repository.GetAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);
        _repository.GetRunningOperationAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(new ManagedServiceOperation());

        var result = await _service.BeginUpgradeAsync(stored.Id, new UpgradeManagedServiceDto { ImageTag = "17" }, _actor, Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public void Plan_contains_secrets_only_in_environment_file_and_firewall_rules()
    {
        var stored = Stored();
        var template = ServiceTemplates.Find(stored.TemplateKey)!;
        var credentials = new ServiceCredentials { Username = "app", Password = "Str0ngPassword123", Database = "app" };

        var plan = _service.BuildPlan(stored, template, credentials, "API_KEY=abcdefghijk\n", "17", replaceExisting: true);

        Assert.Contains("POSTGRES_PASSWORD=Str0ngPassword123\n", plan.EnvironmentFile);
        Assert.Contains("API_KEY=abcdefghijk\n", plan.EnvironmentFile);
        Assert.Contains("Str0ngPassword123", plan.Secrets);
        Assert.Contains("abcdefghijk", plan.Secrets);
        Assert.Equal(["sm-services", "sm-proxy", "app_default"], plan.Networks);
        Assert.Equal(["sm-services", "sm-proxy"], plan.ManagedNetworks);
        Assert.True(plan.Firewall.HasRules);
        Assert.Equal([15432], plan.Firewall.Ports);
        Assert.Equal(["203.0.113.20/32"], plan.Firewall.AllowedSources);
        Assert.Equal("0.0.0.0", Assert.Single(plan.Ports).BindAddress);
        Assert.DoesNotContain("Str0ngPassword123", plan.ToString());
    }

    [Fact]
    public void Firewall_plan_is_empty_when_not_exposed()
    {
        var stored = Stored();
        stored.ExposePublicly = false;

        var plan = ManagedServiceService.FirewallPlan(stored, ServiceTemplates.Find(stored.TemplateKey)!);

        Assert.False(plan.HasRules);
        Assert.Equal("sm-svc-ana-db", plan.Tag);
    }
}
