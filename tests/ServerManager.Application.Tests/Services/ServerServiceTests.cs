using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Tests.TestData;
using ServerManager.Application.Validators.Servers;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class ServerServiceTests
{
    private readonly IServerRepository _repository = Substitute.For<IServerRepository>();
    private readonly FakeSecretProtector _protector = new();
    private readonly ISshConnectionTester _sshTester = Substitute.For<ISshConnectionTester>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ServerService _service;

    public ServerServiceTests()
    {
        _currentUser.UserName.Returns("admin@example.com");
        _service = new ServerService(
            _repository,
            _protector,
            _sshTester,
            _auditLog,
            _currentUser,
            new CreateServerDtoValidator(),
            new UpdateServerDtoValidator(),
            NullLogger<ServerService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_encrypts_secrets_and_writes_audit_log()
    {
        Server? added = null;
        await _repository.AddAsync(Arg.Do<Server>(s => added = s), Arg.Any<CancellationToken>());
        var dto = ServerTestData.ValidCreateDto();
        dto.UseSudo = true;
        dto.SudoPassword = "sudo-pass";

        var result = await _service.CreateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(added.Id, result.Data);
        Assert.NotNull(added.Credential);
        Assert.NotEqual(dto.Password, added.Credential.EncryptedPassword);
        Assert.Equal(dto.Password, _protector.Unprotect(added.Credential.EncryptedPassword!));
        Assert.Equal("sudo-pass", _protector.Unprotect(added.Credential.EncryptedSudoPassword!));
        Assert.Null(added.Credential.EncryptedPrivateKey);
        Assert.Equal(["production", "web"], added.Tags.Select(t => t.Name));
        Assert.Equal("admin@example.com", added.CreatedBy);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ServerCreate && e.EntityId == added.Id.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_audit_details_never_contain_secrets()
    {
        var dto = ServerTestData.ValidCreateDto();

        await _service.CreateAsync(dto, Ct);

        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Details == null || !e.Details.Contains(dto.Password!)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ignores_secrets_not_used_by_authentication_type()
    {
        Server? added = null;
        await _repository.AddAsync(Arg.Do<Server>(s => added = s), Arg.Any<CancellationToken>());
        var dto = ServerTestData.ValidCreateDto();
        dto.AuthenticationType = AuthenticationType.PrivateKey;
        dto.PrivateKey = ServerTestData.SamplePrivateKey;
        dto.SudoPassword = "should-be-dropped";

        var result = await _service.CreateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(added!.Credential!.EncryptedPassword);
        Assert.Null(added.Credential.EncryptedSudoPassword);
        Assert.NotNull(added.Credential.EncryptedPrivateKey);
    }

    [Fact]
    public async Task Create_links_cloud_server_and_audits_import()
    {
        Server? added = null;
        await _repository.AddAsync(Arg.Do<Server>(s => added = s), Arg.Any<CancellationToken>());
        var accountId = Guid.NewGuid();
        _repository.CloudAccountExistsAsync(accountId, Arg.Any<CancellationToken>()).Returns(true);
        var dto = ServerTestData.ValidCreateDto();
        dto.CloudAccountId = accountId;
        dto.CloudExternalId = " 4711 ";

        var result = await _service.CreateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(accountId, added!.CloudAccountId);
        Assert.Equal("4711", added.CloudExternalId);
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.CloudImport), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_already_linked_cloud_server()
    {
        var accountId = Guid.NewGuid();
        _repository.CloudAccountExistsAsync(accountId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.CloudLinkExistsAsync(accountId, "4711", Arg.Any<CancellationToken>()).Returns(true);
        var dto = ServerTestData.ValidCreateDto();
        dto.CloudAccountId = accountId;
        dto.CloudExternalId = "4711";

        var result = await _service.CreateAsync(dto, Ct);

        Assert.False(result.IsSuccess);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Server>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_unknown_cloud_account()
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.CloudAccountId = Guid.NewGuid();
        dto.CloudExternalId = "4711";

        var result = await _service.CreateAsync(dto, Ct);

        Assert.False(result.IsSuccess);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Server>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_duplicate_name()
    {
        _repository.NameExistsAsync("Production-01", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.CreateAsync(ServerTestData.ValidCreateDto(), Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateServerDto.Name));
        await _repository.DidNotReceive().AddAsync(Arg.Any<Server>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_with_invalid_input_does_not_touch_repository()
    {
        var dto = ServerTestData.ValidCreateDto();
        dto.IpAddress = "invalid";

        var result = await _service.CreateAsync(dto, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Server>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_with_blank_secret_keeps_existing_password()
    {
        var server = CreateExistingServer();
        var originalEncrypted = server.Credential!.EncryptedPassword;

        var result = await _service.UpdateAsync(ServerTestData.ValidUpdateDto(server.Id), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(originalEncrypted, server.Credential.EncryptedPassword);
        Assert.Equal("SHA256:existing", server.HostKeyFingerprint);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_with_new_secret_replaces_encrypted_value()
    {
        var server = CreateExistingServer();
        var dto = ServerTestData.ValidUpdateDto(server.Id);
        dto.Password = "new-password";

        var result = await _service.UpdateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-password", _protector.Unprotect(server.Credential!.EncryptedPassword!));
        Assert.Equal("admin@example.com", server.Credential.UpdatedBy);
    }

    [Fact]
    public async Task Update_switching_to_private_key_without_key_fails()
    {
        var server = CreateExistingServer();
        var dto = ServerTestData.ValidUpdateDto(server.Id);
        dto.AuthenticationType = AuthenticationType.PrivateKey;

        var result = await _service.UpdateAsync(dto, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ServerFormDto.PrivateKey));
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_host_change_resets_fingerprint_and_status()
    {
        var server = CreateExistingServer();
        var dto = ServerTestData.ValidUpdateDto(server.Id);
        dto.IpAddress = "203.0.113.99";

        var result = await _service.UpdateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(server.HostKeyFingerprint);
        Assert.Equal(ServerStatus.Unknown, server.Status);
    }

    [Fact]
    public async Task Update_syncs_tags()
    {
        var server = CreateExistingServer();
        var dto = ServerTestData.ValidUpdateDto(server.Id);
        dto.Tags = "web, eu";

        await _service.UpdateAsync(dto, Ct);

        Assert.Equal(["eu", "web"], server.Tags.Select(t => t.Name).Order());
    }

    [Fact]
    public async Task Update_unknown_server_returns_not_found()
    {
        var result = await _service.UpdateAsync(ServerTestData.ValidUpdateDto(Guid.NewGuid()), Ct);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Delete_requires_exact_server_name()
    {
        var server = CreateExistingServer();

        var result = await _service.DeleteAsync(server.Id, "production-01", Ct);

        Assert.False(result.IsSuccess);
        Assert.False(server.IsDeleted);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_with_confirmation_soft_deletes_and_audits()
    {
        var server = CreateExistingServer();

        var result = await _service.DeleteAsync(server.Id, "Production-01", Ct);

        Assert.True(result.IsSuccess);
        Assert.True(server.IsDeleted);
        Assert.NotNull(server.DeletedAt);
        Assert.Equal("admin@example.com", server.DeletedBy);
        _repository.DidNotReceive().Remove(Arg.Any<Server>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ServerDelete),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnection_trusts_fingerprint_on_first_success()
    {
        var server = CreateExistingServer();
        server.HostKeyFingerprint = null;
        server.OperatingSystem = null;
        SshConnectionRequest? sentRequest = null;
        _sshTester.TestAsync(Arg.Do<SshConnectionRequest>(r => sentRequest = r), Arg.Any<CancellationToken>())
            .Returns(new SshConnectionTestResult
            {
                IsSuccess = true,
                Message = "Bağlantı başarılı.",
                HostKeyFingerprint = "SHA256:new",
                OperatingSystem = "Ubuntu 24.04 LTS"
            });

        var result = await _service.TestConnectionAsync(server.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.FingerprintTrustedNow);
        Assert.Equal("SHA256:new", server.HostKeyFingerprint);
        Assert.Equal("Ubuntu 24.04 LTS", server.OperatingSystem);
        Assert.Equal(ServerStatus.Healthy, server.Status);
        Assert.True(server.LastConnectionSucceeded);
        Assert.Equal("old-password", sentRequest!.Password);
        Assert.Null(sentRequest.ExpectedHostKeyFingerprint);
    }

    [Fact]
    public async Task TestConnection_sends_stored_fingerprint_and_marks_offline_on_failure()
    {
        var server = CreateExistingServer();
        SshConnectionRequest? sentRequest = null;
        _sshTester.TestAsync(Arg.Do<SshConnectionRequest>(r => sentRequest = r), Arg.Any<CancellationToken>())
            .Returns(new SshConnectionTestResult
            {
                IsSuccess = false,
                Message = "Host key değişmiş.",
                HostKeyFingerprint = "SHA256:attacker",
                FingerprintMismatch = true
            });

        var result = await _service.TestConnectionAsync(server.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsSuccess);
        Assert.True(result.Data.FingerprintMismatch);
        Assert.Equal("SHA256:existing", sentRequest!.ExpectedHostKeyFingerprint);
        Assert.Equal("SHA256:existing", server.HostKeyFingerprint);
        Assert.Equal(ServerStatus.Offline, server.Status);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ServerConnectionTest && !e.IsSuccess),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnection_keeps_maintenance_status()
    {
        var server = CreateExistingServer();
        server.Status = ServerStatus.Maintenance;
        _sshTester.TestAsync(Arg.Any<SshConnectionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SshConnectionTestResult { IsSuccess = false, Message = "Zaman aşımı." });

        await _service.TestConnectionAsync(server.Id, Ct);

        Assert.Equal(ServerStatus.Maintenance, server.Status);
    }

    [Fact]
    public async Task TestConnection_returns_failure_when_secret_cannot_be_decrypted()
    {
        var server = CreateExistingServer();
        _protector.FailOnUnprotect = true;

        var result = await _service.TestConnectionAsync(server.Id, Ct);

        Assert.False(result.IsSuccess);
        await _sshTester.DidNotReceive().TestAsync(Arg.Any<SshConnectionRequest>(), Arg.Any<CancellationToken>());
    }

    private Server CreateExistingServer()
    {
        var server = new Server
        {
            Name = "Production-01",
            Hostname = "web-01.example.com",
            IpAddress = "203.0.113.10",
            SshPort = 22,
            Username = "deploy",
            AuthenticationType = AuthenticationType.Password,
            Environment = ServerEnvironment.Production,
            Status = ServerStatus.Healthy,
            HostKeyFingerprint = "SHA256:existing",
            OperatingSystem = "Debian 12"
        };
        server.Credential = new ServerCredential
        {
            ServerId = server.Id,
            EncryptedPassword = _protector.Protect("old-password")
        };
        server.Tags.Add(new ServerTag { ServerId = server.Id, Name = "production" });
        server.Tags.Add(new ServerTag { ServerId = server.Id, Name = "web" });

        _repository.GetWithDetailsAsync(server.Id, Arg.Any<CancellationToken>()).Returns(server);
        return server;
    }
}
