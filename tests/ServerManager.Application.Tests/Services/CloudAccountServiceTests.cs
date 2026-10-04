using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Cloud;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Validators.Cloud;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class CloudAccountServiceTests
{
    private const string ProviderName = "Cloud.Test";
    private const string Token = "test-token-123";
    private const string Key = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIOMqqnkVzrm0SdG6UOoqKLsabgH5C9okWi0dh2l9GKJl ops@laptop";

    private readonly ICloudAccountRepository _repository = Substitute.For<ICloudAccountRepository>();
    private readonly IServerTemplateRepository _templates = Substitute.For<IServerTemplateRepository>();
    private readonly ICloudProviderRegistry _registry = Substitute.For<ICloudProviderRegistry>();
    private readonly ICloudProvider _provider = Substitute.For<ICloudProvider>();
    private readonly FakeSecretProtector _protector = new();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly CloudAccountService _service;

    public CloudAccountServiceTests()
    {
        _currentUser.UserName.Returns("admin@example.com");
        _provider.SystemName.Returns(ProviderName);
        _provider.DisplayName.Returns("Test Bulut");
        _provider.Currency.Returns("EUR");
        _registry.Find(Arg.Any<string>()).Returns((ICloudProvider?)null);
        _registry.Find(ProviderName).Returns(_provider);
        _registry.FindDisplayName(ProviderName).Returns("Test Bulut");
        _repository.GetUnlinkedServersAsync(Arg.Any<CancellationToken>()).Returns(new List<Server>());
        _service = new CloudAccountService(_repository, _templates, _registry, _protector, _auditLog, _currentUser,
            new CloudAccountFormDtoValidator(), new CloudProvisionDtoValidator(),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero)), NullLogger<CloudAccountService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CloudAccount Account(params Server[] servers)
    {
        var account = new CloudAccount { Name = "Hesap", Provider = ProviderName, EncryptedToken = _protector.Protect(Token) };
        foreach (var server in servers)
        {
            server.CloudAccountId = account.Id;
            account.Servers.Add(server);
        }
        _repository.GetAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _repository.GetWithServersAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        return account;
    }

    private void Remote(params CloudServerInfo[] servers) =>
        _provider.ListServersAsync(Token, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<CloudServerInfo>>.Success(servers));

    private static CloudServerInfo Info(string id, string ip, decimal? price = 4.51m) =>
        new(id, "srv-" + id, "running", ip, "fsn1", "cx22", price, "EUR", null);

    [Fact]
    public async Task Create_validates_token_and_stores_it_encrypted()
    {
        CloudAccount? added = null;
        await _repository.AddAsync(Arg.Do<CloudAccount>(a => added = a), Arg.Any<CancellationToken>());
        _provider.ValidateTokenAsync(Token, Arg.Any<CancellationToken>()).Returns(ServiceResult<string>.Success("Proje #42"));

        var result = await _service.CreateAsync(new CloudAccountFormDto { Name = " Ana hesap ", Provider = ProviderName, Token = $" {Token} " }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ana hesap", added!.Name);
        Assert.NotEqual(Token, added.EncryptedToken);
        Assert.Equal(Token, _protector.Unprotect(added.EncryptedToken));
        Assert.Equal("Proje #42", added.AccountLabel);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.CloudAccountCreate && !e.Details!.Contains(Token)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_returns_token_error_when_provider_rejects_it()
    {
        _provider.ValidateTokenAsync(Token, Arg.Any<CancellationToken>()).Returns(ServiceResult<string>.Failure("API anahtarı geçersiz."));

        var result = await _service.CreateAsync(new CloudAccountFormDto { Name = "Hesap", Provider = ProviderName, Token = Token }, Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CloudAccountFormDto.Token) && e.Message == "API anahtarı geçersiz.");
        await _repository.DidNotReceive().AddAsync(Arg.Any<CloudAccount>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_unknown_provider()
    {
        var result = await _service.CreateAsync(new CloudAccountFormDto { Name = "Hesap", Provider = "Cloud.Unknown", Token = Token }, Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CloudAccountFormDto.Provider));
    }

    [Fact]
    public async Task Update_without_token_keeps_existing_token()
    {
        var account = Account();
        var encrypted = account.EncryptedToken;

        var result = await _service.UpdateAsync(new CloudAccountFormDto { Id = account.Id, Name = "Yeni ad" }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Yeni ad", account.Name);
        Assert.Equal(encrypted, account.EncryptedToken);
        await _provider.DidNotReceive().ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_soft_deletes_and_unlinks_servers_without_deleting_them()
    {
        var server = new Server { Name = "web-01", IpAddress = "203.0.113.10", CloudExternalId = "11" };
        var account = Account(server);

        var result = await _service.DeleteAsync(account.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.True(account.IsDeleted);
        Assert.Null(server.CloudAccountId);
        Assert.Null(server.CloudExternalId);
        Assert.False(server.IsDeleted);
    }

    [Fact]
    public async Task Sync_links_unique_ip_match_and_updates_cost()
    {
        var account = Account();
        var match = new Server { Name = "web-01", IpAddress = "203.0.113.10" };
        _repository.GetUnlinkedServersAsync(Arg.Any<CancellationToken>()).Returns(new List<Server> { match });
        Remote(Info("11", "203.0.113.10"), Info("12", "203.0.113.99"));

        var result = await _service.SyncAsync(account.Id, cancellationToken: Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(new CloudSyncSummary(2, 1, 1, 1), result.Data);
        Assert.Equal(account.Id, match.CloudAccountId);
        Assert.Equal("11", match.CloudExternalId);
        Assert.Equal(4.51m, match.MonthlyCost);
        Assert.Equal("EUR", match.CostCurrency);
        Assert.NotNull(account.LastSyncAt);
        Assert.Null(account.LastSyncError);
    }

    [Fact]
    public async Task Sync_does_not_link_when_ip_matches_multiple_servers()
    {
        var account = Account();
        var first = new Server { Name = "a", IpAddress = "203.0.113.10" };
        var second = new Server { Name = "b", IpAddress = "203.0.113.10" };
        _repository.GetUnlinkedServersAsync(Arg.Any<CancellationToken>()).Returns(new List<Server> { first, second });
        Remote(Info("11", "203.0.113.10"));

        var result = await _service.SyncAsync(account.Id, cancellationToken: Ct);

        Assert.Equal(0, result.Data!.NewlyLinkedCount);
        Assert.Null(first.CloudAccountId);
        Assert.Null(second.CloudAccountId);
    }

    [Fact]
    public async Task Sync_keeps_manual_cost_when_provider_has_no_price()
    {
        var server = new Server { Name = "web-01", IpAddress = "203.0.113.10", CloudExternalId = "11", MonthlyCost = 9m, CostCurrency = "USD" };
        var account = Account(server);
        Remote(Info("11", "203.0.113.10", price: null));

        await _service.SyncAsync(account.Id, cancellationToken: Ct);

        Assert.Equal(9m, server.MonthlyCost);
        Assert.Equal("USD", server.CostCurrency);
    }

    [Fact]
    public async Task Sync_records_error_and_audits_failure_when_manual()
    {
        var account = Account();
        _provider.ListServersAsync(Token, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<CloudServerInfo>>.Failure("API anahtarı geçersiz."));

        var result = await _service.SyncAsync(account.Id, cancellationToken: Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("API anahtarı geçersiz.", account.LastSyncError);
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.CloudSync && !e.IsSuccess), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Automatic_sync_without_changes_is_not_audited()
    {
        var server = new Server { Name = "web-01", IpAddress = "203.0.113.10", CloudExternalId = "11", MonthlyCost = 4.51m, CostCurrency = "EUR" };
        var account = Account(server);
        Remote(Info("11", "203.0.113.10"));

        var result = await _service.SyncAsync(account.Id, automatic: true, Ct);

        Assert.True(result.IsSuccess);
        await _auditLog.DidNotReceive().LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sync_fails_when_provider_plugin_is_disabled()
    {
        var account = Account();
        _registry.Find(ProviderName).Returns((ICloudProvider?)null);

        var result = await _service.SyncAsync(account.Id, cancellationToken: Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("eklentisi", result.Message);
    }

    [Fact]
    public async Task GetServers_marks_linked_servers()
    {
        var server = new Server { Name = "web-01", IpAddress = "203.0.113.10", CloudExternalId = "11" };
        var account = Account(server);
        Remote(Info("12", "203.0.113.20"), Info("11", "203.0.113.10"));

        var result = await _service.GetServersAsync(account.Id, Ct);

        var linked = Assert.Single(result.Data!.Servers, s => s.LinkedServerId is not null);
        Assert.Equal("11", linked.Server.ExternalId);
        Assert.Equal("web-01", linked.LinkedServerName);
    }

    [Fact]
    public async Task Provision_sends_user_data_with_key_and_audits_without_password()
    {
        var account = Account();
        var template = new ServerTemplate { Name = "nginx", Kind = ServerTemplateKind.CloudInit, Content = "#cloud-config\npackages: [nginx]" };
        _templates.GetAsync(template.Id, Arg.Any<CancellationToken>()).Returns(template);
        CloudCreateServerRequest? sent = null;
        _provider.CreateServerAsync(Token, Arg.Do<CloudCreateServerRequest>(r => sent = r), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CloudCreateResult>.Success(new CloudCreateResult(Info("99", "203.0.113.50"), "Gizli-Parola-1")));

        var result = await _service.ProvisionAsync(new CloudProvisionDto
        {
            AccountId = account.Id, Name = "web-03", Region = "fsn1", Size = "cx22", Image = "ubuntu-24.04", TemplateId = template.Id, SshPublicKey = Key
        }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Gizli-Parola-1", result.Data!.RootPassword);
        Assert.Equal("web-03", sent!.Name);
        Assert.Contains("packages: [nginx]", sent.UserData);
        Assert.Contains($"  - {Key}", sent.UserData);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.CloudProvision && e.IsSuccess && !e.Details!.Contains("Gizli-Parola-1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_rejects_script_template()
    {
        var account = Account();
        var template = new ServerTemplate { Name = "script", Kind = ServerTemplateKind.Script, Content = "uptime" };
        _templates.GetAsync(template.Id, Arg.Any<CancellationToken>()).Returns(template);

        var result = await _service.ProvisionAsync(new CloudProvisionDto
        {
            AccountId = account.Id, Name = "web-03", Region = "fsn1", Size = "cx22", Image = "ubuntu-24.04", TemplateId = template.Id
        }, Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CloudProvisionDto.TemplateId));
        await _provider.DidNotReceive().CreateServerAsync(Arg.Any<string>(), Arg.Any<CloudCreateServerRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_audits_provider_failure()
    {
        var account = Account();
        _provider.CreateServerAsync(Token, Arg.Any<CloudCreateServerRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CloudCreateResult>.Failure("Kota doldu."));

        var result = await _service.ProvisionAsync(new CloudProvisionDto
        {
            AccountId = account.Id, Name = "web-03", Region = "fsn1", Size = "cx22", Image = "ubuntu-24.04"
        }, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("Kota doldu.", result.Message);
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.CloudProvision && !e.IsSuccess), Arg.Any<CancellationToken>());
    }
}
