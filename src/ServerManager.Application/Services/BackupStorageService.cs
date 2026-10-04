using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Backups;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Notifications;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class BackupStorageService : IBackupStorageService
{
    private const string NotFoundMessage = "Depolama hedefi bulunamadı.";

    private readonly IBackupRepository _repository;
    private readonly IBackupStorageRegistry _registry;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<BackupStorageFormDto> _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BackupStorageService> _logger;

    public BackupStorageService(
        IBackupRepository repository,
        IBackupStorageRegistry registry,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<BackupStorageFormDto> validator,
        TimeProvider timeProvider,
        ILogger<BackupStorageService> logger)
    {
        _repository = repository;
        _registry = registry;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<BackupStorageListItemDto>> GetStoragesAsync(CancellationToken cancellationToken = default)
    {
        var storages = await _repository.GetStoragesAsync(cancellationToken);
        var jobCounts = await _repository.GetJobCountsByStorageAsync(cancellationToken);
        return storages.Select(s => new BackupStorageListItemDto(
            s.Id,
            s.Name,
            s.ProviderSystemName,
            _registry.FindDisplayName(s.ProviderSystemName) ?? s.ProviderSystemName,
            _registry.Find(s.ProviderSystemName) is not null,
            jobCounts.GetValueOrDefault(s.Id),
            s.LastTestedAt,
            s.LastError)).ToList();
    }

    public async Task<IReadOnlyList<BackupStorageOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var storages = await _repository.GetStoragesAsync(cancellationToken);
        return storages
            .Select(s => new BackupStorageOptionDto(s.Id, s.Name, _registry.FindDisplayName(s.ProviderSystemName) ?? s.ProviderSystemName))
            .ToList();
    }

    public IReadOnlyList<BackupStorageProviderDto> GetProviders() =>
        _registry.GetEnabled()
            .Select(p => new BackupStorageProviderDto(p.SystemName, p.DisplayName, p.Description, p.Fields))
            .ToList();

    public async Task<ServiceResult<BackupStorageFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var storage = await _repository.GetStorageAsync(id, cancellationToken);
        if (storage is null)
            return ServiceResult<BackupStorageFormDto>.NotFound(NotFoundMessage);

        var dto = new BackupStorageFormDto
        {
            Id = storage.Id,
            Name = storage.Name,
            ProviderSystemName = storage.ProviderSystemName
        };

        var stored = TryReadSettings(storage);
        var provider = _registry.Find(storage.ProviderSystemName);
        if (stored is not null && provider is not null)
        {
            foreach (var field in provider.Fields)
            {
                if (!stored.TryGetValue(field.Key, out var value) || string.IsNullOrEmpty(value))
                    continue;

                if (field.IsSecret)
                    dto.StoredSecretKeys.Add(field.Key);
                else
                    dto.Settings[field.Key] = value;
            }
        }

        return ServiceResult<BackupStorageFormDto>.Success(dto);
    }

    public async Task<ServiceResult<Guid>> CreateAsync(BackupStorageFormDto dto, CancellationToken cancellationToken = default)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var provider = _registry.Find(dto.ProviderSystemName);
        if (provider is null)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.ProviderSystemName), "Depolama türü bulunamadı veya eklentisi etkin değil.");

        if (await _repository.StorageNameExistsAsync(dto.Name, null, cancellationToken))
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Name), "Bu isimde bir depolama hedefi zaten kayıtlı.");

        var (settings, errors) = ProviderSettingsBuilder.Build(provider.Fields, provider.Validate, dto.Settings, null);
        if (errors.Count > 0)
            return ServiceResult<Guid>.ValidationFailure(errors);

        var storage = new BackupStorage
        {
            Name = dto.Name,
            ProviderSystemName = provider.SystemName,
            EncryptedSettings = _secretProtector.Protect(NotificationSettingsSerializer.Serialize(settings)),
            CreatedAt = UtcNow,
            CreatedBy = _currentUser.UserName
        };

        await _repository.AddStorageAsync(storage, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Yedek depolama eklendi. StorageId: {StorageId}, Provider: {Provider}", storage.Id, storage.ProviderSystemName);
        await AuditAsync(AuditActions.BackupStorageCreate, storage, $"Tür: {provider.DisplayName}", true, cancellationToken);
        return ServiceResult<Guid>.Success(storage.Id, "Depolama hedefi eklendi. Yedekleme işinde kullanmadan önce testi çalıştırın.");
    }

    public async Task<ServiceResult> UpdateAsync(BackupStorageFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var storage = await _repository.GetStorageAsync(id, cancellationToken);
        if (storage is null)
            return ServiceResult.NotFound(NotFoundMessage);

        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.ProviderSystemName = storage.ProviderSystemName;
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var provider = _registry.Find(storage.ProviderSystemName);
        if (provider is null)
            return ServiceResult.Failure("Depolamanın eklentisi etkin değil; önce eklentiyi etkinleştirin.");

        if (await _repository.StorageNameExistsAsync(dto.Name, storage.Id, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), "Bu isimde bir depolama hedefi zaten kayıtlı.");

        var stored = TryReadSettings(storage);
        if (stored is null && provider.Fields.Any(f => f.IsSecret && string.IsNullOrEmpty(dto.Settings.GetValueOrDefault(f.Key))))
            return ServiceResult.Failure("Kayıtlı ayarlar çözülemedi; gizli alanları yeniden girin.");

        var (settings, errors) = ProviderSettingsBuilder.Build(provider.Fields, provider.Validate, dto.Settings, stored);
        if (errors.Count > 0)
            return ServiceResult.ValidationFailure(errors);

        var changes = new List<string>();
        if (storage.Name != dto.Name) changes.Add($"Ad: {storage.Name} -> {dto.Name}");
        if (stored is null || !ProviderSettingsBuilder.AreEqual(stored, settings)) changes.Add("Ayarlar güncellendi");

        storage.Name = dto.Name;
        storage.EncryptedSettings = _secretProtector.Protect(NotificationSettingsSerializer.Serialize(settings));
        storage.UpdatedAt = UtcNow;
        storage.UpdatedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.BackupStorageUpdate, storage, changes.Count == 0 ? "Değişiklik yok" : string.Join(" | ", changes), true, cancellationToken);
        return ServiceResult.Success("Depolama hedefi güncellendi. Yeni ayarlar sonraki yedeklerde kullanılır; önceki yedekler eski konumdan okunur.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var storage = await _repository.GetStorageAsync(id, cancellationToken);
        if (storage is null)
            return ServiceResult.NotFound(NotFoundMessage);

        var jobCounts = await _repository.GetJobCountsByStorageAsync(cancellationToken);
        if (jobCounts.GetValueOrDefault(storage.Id) > 0)
            return ServiceResult.Failure("Bu depolamayı kullanan yedekleme işleri var; önce işleri başka depolamaya taşıyın veya silin.", ServiceErrorType.Conflict);

        storage.IsDeleted = true;
        storage.DeletedAt = UtcNow;
        storage.DeletedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.BackupStorageDelete, storage, null, true, cancellationToken);
        return ServiceResult.Success("Depolama hedefi silindi. Depolamadaki yedek dosyalarına dokunulmadı; geçmişteki yedekler yine geri yüklenebilir.");
    }

    public async Task<ServiceResult> TestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var storage = await _repository.GetStorageAsync(id, cancellationToken);
        if (storage is null)
            return ServiceResult.NotFound(NotFoundMessage);

        var target = Resolve(storage);
        var result = target.IsSuccess
            ? await target.Data!.Provider.TestAsync(target.Data.Settings, cancellationToken)
            : ServiceResult.Failure(target.Message ?? "Depolama hazırlanamadı.");

        storage.LastTestedAt = UtcNow;
        storage.LastError = result.IsSuccess ? null : TextHelper.Truncate(result.Message, 500);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.BackupStorageTest, storage, result.Message, result.IsSuccess, cancellationToken);
        return result.IsSuccess
            ? ServiceResult.Success(result.Message ?? "Depolama testi başarılı: yazma, okuma ve silme çalışıyor.")
            : ServiceResult.Failure(result.Message ?? "Depolama testi başarısız.");
    }

    public ServiceResult<BackupStorageTarget> Resolve(BackupStorage storage)
    {
        var provider = _registry.Find(storage.ProviderSystemName);
        if (provider is null)
            return ServiceResult<BackupStorageTarget>.Failure($"{storage.Name} depolamasının eklentisi kurulu veya etkin değil.");

        var settings = TryReadSettings(storage);
        if (settings is null)
            return ServiceResult<BackupStorageTarget>.Failure($"{storage.Name} depolamasının ayarları çözülemedi (Security:MasterKey değişmiş olabilir).");

        return ServiceResult<BackupStorageTarget>.Success(new BackupStorageTarget(storage.Id, storage.Name, provider, settings));
    }

    private Dictionary<string, string>? TryReadSettings(BackupStorage storage)
    {
        try
        {
            return NotificationSettingsSerializer.Deserialize(_secretProtector.Unprotect(storage.EncryptedSettings));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            _logger.LogWarning(ex, "Yedek depolama ayarları çözülemedi. StorageId: {StorageId}", storage.Id);
            return null;
        }
    }

    private Task AuditAsync(string action, BackupStorage storage, string? details, bool isSuccess, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.BackupStorage, storage.Id.ToString(), storage.Name, details, isSuccess), cancellationToken);
}
