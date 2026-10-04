using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class ServerService : IServerService
{
    private const string NotFoundMessage = "Sunucu bulunamadı.";

    private readonly IServerRepository _serverRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly ISshConnectionTester _sshConnectionTester;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateServerDto> _createValidator;
    private readonly IValidator<UpdateServerDto> _updateValidator;
    private readonly ILogger<ServerService> _logger;

    public ServerService(
        IServerRepository serverRepository,
        ISecretProtector secretProtector,
        ISshConnectionTester sshConnectionTester,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<CreateServerDto> createValidator,
        IValidator<UpdateServerDto> updateValidator,
        ILogger<ServerService> logger)
    {
        _serverRepository = serverRepository;
        _secretProtector = secretProtector;
        _sshConnectionTester = sshConnectionTester;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public async Task<PagedResult<ServerListItemDto>> SearchAsync(ServerFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = await _serverRepository.SearchAsync(filter, cancellationToken);
        return page.Map(s => s.ToListItemDto());
    }

    public async Task<ServiceResult<ServerDetailsDto>> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetWithDetailsAsync(id, cancellationToken);
        return server is null
            ? ServiceResult<ServerDetailsDto>.NotFound(NotFoundMessage)
            : ServiceResult<ServerDetailsDto>.Success(server.ToDetailsDto());
    }

    public async Task<ServiceResult<UpdateServerDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetWithDetailsAsync(id, cancellationToken);
        return server is null
            ? ServiceResult<UpdateServerDto>.NotFound(NotFoundMessage)
            : ServiceResult<UpdateServerDto>.Success(server.ToUpdateDto());
    }

    public async Task<ServiceResult<Guid>> CreateAsync(CreateServerDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        if (await _serverRepository.NameExistsAsync(dto.Name.Trim(), null, cancellationToken))
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Name), "Bu isimde bir sunucu zaten kayıtlı.");

        if (dto.GroupId is { } createGroupId && !await _serverRepository.GroupExistsAsync(createGroupId, cancellationToken))
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.GroupId), "Seçilen grup bulunamadı.");

        var userName = _currentUser.UserName;
        var server = new Server { CreatedBy = userName };
        ApplyFormValues(server, dto);

        var credential = new ServerCredential
        {
            ServerId = server.Id,
            CreatedBy = userName,
            KeyVersion = _secretProtector.KeyVersion
        };
        ApplySecrets(credential, dto);
        server.Credential = credential;

        foreach (var tag in TagParser.Parse(dto.Tags))
            server.Tags.Add(new ServerTag { ServerId = server.Id, Name = tag });

        await _serverRepository.AddAsync(server, cancellationToken);
        await _serverRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sunucu eklendi. ServerId: {ServerId}, Name: {ServerName}", server.Id, server.Name);
        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.ServerCreate,
            AuditEntityTypes.Server,
            server.Id.ToString(),
            server.Name,
            $"{server.Username}@{server.IpAddress}:{server.SshPort}"), cancellationToken);

        return ServiceResult<Guid>.Success(server.Id, "Sunucu eklendi.");
    }

    public async Task<ServiceResult> UpdateAsync(UpdateServerDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var server = await _serverRepository.GetWithDetailsAsync(dto.Id, cancellationToken);
        if (server is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (await _serverRepository.NameExistsAsync(dto.Name.Trim(), server.Id, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), "Bu isimde bir sunucu zaten kayıtlı.");

        if (dto.GroupId is { } updateGroupId && updateGroupId != server.GroupId && !await _serverRepository.GroupExistsAsync(updateGroupId, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.GroupId), "Seçilen grup bulunamadı.");

        var hostChanged =
            !string.Equals(server.IpAddress, dto.IpAddress.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(server.Hostname, dto.Hostname.Trim(), StringComparison.OrdinalIgnoreCase)
            || server.SshPort != dto.SshPort;

        var userName = _currentUser.UserName;
        var now = DateTime.UtcNow;

        ApplyFormValues(server, dto);
        server.UpdatedAt = now;
        server.UpdatedBy = userName;

        var credential = server.Credential;
        if (credential is null)
        {
            credential = new ServerCredential { ServerId = server.Id, CreatedBy = userName };
            server.Credential = credential;
        }

        var secretsChanged = ApplySecrets(credential, dto);

        var missingSecretErrors = GetMissingSecretErrors(credential, server.AuthenticationType);
        if (missingSecretErrors.Count > 0)
            return ServiceResult.ValidationFailure(missingSecretErrors);

        if (secretsChanged)
        {
            credential.KeyVersion = _secretProtector.KeyVersion;
            credential.UpdatedAt = now;
            credential.UpdatedBy = userName;
        }

        if (hostChanged)
        {
            server.HostKeyFingerprint = null;
            server.ConsecutiveFailureCount = 0;
            if (server.Status != ServerStatus.Maintenance)
                server.Status = ServerStatus.Unknown;
        }

        SyncTags(server, TagParser.Parse(dto.Tags));

        await _serverRepository.SaveChangesAsync(cancellationToken);

        var details = new List<string>();
        if (hostChanged)
            details.Add("Bağlantı adresi değişti, host key fingerprint sıfırlandı.");
        if (secretsChanged)
            details.Add("Kimlik bilgileri güncellendi.");

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.ServerUpdate,
            AuditEntityTypes.Server,
            server.Id.ToString(),
            server.Name,
            details.Count > 0 ? string.Join(" ", details) : null), cancellationToken);

        return ServiceResult.Success("Sunucu güncellendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, string? confirmationName, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetWithDetailsAsync(id, cancellationToken);
        if (server is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (!string.Equals(confirmationName?.Trim(), server.Name, StringComparison.Ordinal))
            return ServiceResult.ValidationFailure("ConfirmationName", "Silme işlemini onaylamak için sunucu adını birebir yazın.");

        server.IsDeleted = true;
        server.DeletedAt = DateTime.UtcNow;
        server.DeletedBy = _currentUser.UserName;

        await _serverRepository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Sunucu silindi (soft delete). ServerId: {ServerId}, Name: {ServerName}", server.Id, server.Name);
        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.ServerDelete,
            AuditEntityTypes.Server,
            server.Id.ToString(),
            server.Name), cancellationToken);

        return ServiceResult.Success("Sunucu silindi.");
    }

    public async Task<ServiceResult<ConnectionTestResultDto>> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetWithDetailsAsync(id, cancellationToken);
        if (server is null)
            return ServiceResult<ConnectionTestResultDto>.NotFound(NotFoundMessage);

        var credential = server.Credential;
        if (credential is null)
            return ServiceResult<ConnectionTestResultDto>.Failure("Bu sunucu için kayıtlı kimlik bilgisi bulunamadı.");

        SshConnectionRequest request;
        try
        {
            request = new SshConnectionRequest
            {
                Host = server.IpAddress,
                Port = server.SshPort,
                Username = server.Username,
                AuthenticationType = server.AuthenticationType,
                Password = Decrypt(credential.EncryptedPassword),
                PrivateKey = Decrypt(credential.EncryptedPrivateKey),
                Passphrase = Decrypt(credential.EncryptedPassphrase),
                ExpectedHostKeyFingerprint = server.HostKeyFingerprint
            };
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Sunucu kimlik bilgileri çözülemedi. ServerId: {ServerId}", server.Id);
            return ServiceResult<ConnectionTestResultDto>.Failure("Kimlik bilgileri çözülemedi. Master key değişmiş olabilir.");
        }

        var result = await _sshConnectionTester.TestAsync(request, cancellationToken);
        var now = DateTime.UtcNow;

        var trustedNow = false;
        if (result.IsSuccess && server.HostKeyFingerprint is null && result.HostKeyFingerprint is not null)
        {
            server.HostKeyFingerprint = result.HostKeyFingerprint;
            trustedNow = true;
        }

        if (result.IsSuccess && string.IsNullOrWhiteSpace(server.OperatingSystem) && !string.IsNullOrWhiteSpace(result.OperatingSystem))
            server.OperatingSystem = TextHelper.Truncate(result.OperatingSystem, 128);

        server.LastConnectionTestAt = now;
        server.LastConnectionSucceeded = result.IsSuccess;
        server.LastConnectionMessage = TextHelper.Truncate(result.Message, 500);

        if (result.IsSuccess)
        {
            server.LastSeenAt = now;
            server.ConsecutiveFailureCount = 0;
        }

        if (server.Status != ServerStatus.Maintenance)
        {
            // Başarılı testte izleme eşiklerinden gelen Warning/Critical durumu korunur.
            server.Status = result.IsSuccess
                ? server.Status is ServerStatus.Unknown or ServerStatus.Offline ? ServerStatus.Healthy : server.Status
                : ServerStatus.Offline;
        }

        await _serverRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bağlantı testi tamamlandı. ServerId: {ServerId}, Success: {Success}, DurationMs: {DurationMs}",
            server.Id, result.IsSuccess, result.DurationMs);

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.ServerConnectionTest,
            AuditEntityTypes.Server,
            server.Id.ToString(),
            server.Name,
            result.Message,
            result.IsSuccess), cancellationToken);

        return ServiceResult<ConnectionTestResultDto>.Success(new ConnectionTestResultDto
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            HostKeyFingerprint = server.HostKeyFingerprint ?? result.HostKeyFingerprint,
            FingerprintTrustedNow = trustedNow,
            FingerprintMismatch = result.FingerprintMismatch,
            DurationMs = result.DurationMs,
            TestedAt = now,
            Status = server.Status
        });
    }

    public async Task<IReadOnlyList<ServerOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.FindAsync(_ => true, cancellationToken);
        return servers
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new ServerOptionDto(s.Id, s.Name, s.IpAddress, s.GroupId))
            .ToList();
    }

    public Task<IReadOnlyList<string>> GetTagNamesAsync(CancellationToken cancellationToken = default) =>
        _serverRepository.GetTagNamesAsync(cancellationToken);

    private static void ApplyFormValues(Server server, ServerFormDto dto)
    {
        server.Name = dto.Name.Trim();
        server.Hostname = dto.Hostname.Trim().ToLowerInvariant();
        server.IpAddress = dto.IpAddress.Trim();
        server.SshPort = dto.SshPort;
        server.Username = dto.Username.Trim();
        server.AuthenticationType = dto.AuthenticationType;
        server.UseSudo = dto.UseSudo;
        server.MonitoringEnabled = dto.MonitoringEnabled;
        server.Description = TextHelper.NullIfEmpty(dto.Description);
        server.Environment = dto.Environment;
        server.Location = TextHelper.NullIfEmpty(dto.Location);
        server.Provider = TextHelper.NullIfEmpty(dto.Provider);
        server.OperatingSystem = TextHelper.NullIfEmpty(dto.OperatingSystem);
        server.GroupId = dto.GroupId;
        server.MonthlyCost = dto.MonthlyCost;
        server.CostCurrency = dto.MonthlyCost.HasValue
            ? (string.IsNullOrWhiteSpace(dto.CostCurrency) ? CostCurrencies.Default : dto.CostCurrency.Trim().ToUpperInvariant())
            : null;
    }

    private bool ApplySecrets(ServerCredential credential, ServerFormDto dto)
    {
        var changed = false;

        if (!string.IsNullOrEmpty(dto.Password))
        {
            credential.EncryptedPassword = _secretProtector.Protect(dto.Password);
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(dto.PrivateKey))
        {
            credential.EncryptedPrivateKey = _secretProtector.Protect(NormalizePrivateKey(dto.PrivateKey));
            changed = true;
        }

        if (!string.IsNullOrEmpty(dto.Passphrase))
        {
            credential.EncryptedPassphrase = _secretProtector.Protect(dto.Passphrase);
            changed = true;
        }

        if (!string.IsNullOrEmpty(dto.SudoPassword))
        {
            credential.EncryptedSudoPassword = _secretProtector.Protect(dto.SudoPassword);
            changed = true;
        }

        changed |= ClearUnusedSecrets(credential, dto.AuthenticationType, dto.UseSudo);
        return changed;
    }

    private static bool ClearUnusedSecrets(ServerCredential credential, AuthenticationType authenticationType, bool useSudo)
    {
        var changed = false;

        if (authenticationType != AuthenticationType.Password && credential.EncryptedPassword is not null)
        {
            credential.EncryptedPassword = null;
            changed = true;
        }

        if (authenticationType == AuthenticationType.Password && credential.EncryptedPrivateKey is not null)
        {
            credential.EncryptedPrivateKey = null;
            changed = true;
        }

        if (authenticationType != AuthenticationType.PrivateKeyWithPassphrase && credential.EncryptedPassphrase is not null)
        {
            credential.EncryptedPassphrase = null;
            changed = true;
        }

        if (!useSudo && credential.EncryptedSudoPassword is not null)
        {
            credential.EncryptedSudoPassword = null;
            changed = true;
        }

        return changed;
    }

    private static List<ServiceError> GetMissingSecretErrors(ServerCredential credential, AuthenticationType authenticationType)
    {
        var errors = new List<ServiceError>();

        if (authenticationType == AuthenticationType.Password && credential.EncryptedPassword is null)
            errors.Add(new ServiceError(nameof(ServerFormDto.Password), "Parola ile kimlik doğrulamada SSH parolası zorunludur."));

        if (authenticationType is AuthenticationType.PrivateKey or AuthenticationType.PrivateKeyWithPassphrase
            && credential.EncryptedPrivateKey is null)
            errors.Add(new ServiceError(nameof(ServerFormDto.PrivateKey), "Private key ile kimlik doğrulamada private key zorunludur."));

        if (authenticationType == AuthenticationType.PrivateKeyWithPassphrase && credential.EncryptedPassphrase is null)
            errors.Add(new ServiceError(nameof(ServerFormDto.Passphrase), "Passphrase korumalı key için passphrase zorunludur."));

        return errors;
    }

    private static void SyncTags(Server server, IReadOnlyList<string> tags)
    {
        var toRemove = server.Tags.Where(t => !tags.Contains(t.Name)).ToList();
        foreach (var tag in toRemove)
            server.Tags.Remove(tag);

        foreach (var name in tags.Where(n => server.Tags.All(t => t.Name != n)))
            server.Tags.Add(new ServerTag { ServerId = server.Id, Name = name });
    }

    private string? Decrypt(string? protectedValue) =>
        protectedValue is null ? null : _secretProtector.Unprotect(protectedValue);

    private static string NormalizePrivateKey(string privateKey) =>
        privateKey.Replace("\r\n", "\n", StringComparison.Ordinal).Trim() + "\n";
}
