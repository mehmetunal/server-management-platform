using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Notifications;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class NotificationChannelService : INotificationChannelService
{
    private const string NotFoundMessage = "Bildirim kanalı bulunamadı.";
    private const string SettingsPrefix = "Settings";

    private readonly IAlertRepository _repository;
    private readonly INotificationChannelRegistry _registry;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<NotificationChannelFormDto> _validator;
    private readonly TimeProvider _timeProvider;
    private readonly AlertingOptions _options;
    private readonly ILogger<NotificationChannelService> _logger;

    public NotificationChannelService(
        IAlertRepository repository,
        INotificationChannelRegistry registry,
        INotificationDispatcher dispatcher,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<NotificationChannelFormDto> validator,
        TimeProvider timeProvider,
        IOptions<AlertingOptions> options,
        ILogger<NotificationChannelService> logger)
    {
        _repository = repository;
        _registry = registry;
        _dispatcher = dispatcher;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<NotificationChannelListItemDto>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var channels = await _repository.GetChannelsAsync(cancellationToken);
        var ruleCounts = await _repository.GetRuleCountsByChannelAsync(cancellationToken);
        return channels.Select(c => new NotificationChannelListItemDto(
            c.Id,
            c.Name,
            c.ProviderSystemName,
            _registry.FindDisplayName(c.ProviderSystemName) ?? c.ProviderSystemName,
            _registry.Find(c.ProviderSystemName) is not null,
            c.MinimumSeverity,
            c.IsEnabled,
            c.LastSentAt,
            c.LastError,
            ruleCounts.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<IReadOnlyList<ChannelOptionDto>> GetChannelOptionsAsync(CancellationToken cancellationToken = default)
    {
        var channels = await _repository.GetChannelsAsync(cancellationToken);
        return channels
            .Select(c => new ChannelOptionDto(c.Id, c.Name, _registry.FindDisplayName(c.ProviderSystemName) ?? c.ProviderSystemName, c.IsEnabled, c.MinimumSeverity))
            .ToList();
    }

    public IReadOnlyList<NotificationProviderDto> GetProviders() =>
        _registry.GetEnabled()
            .Select(p => new NotificationProviderDto(p.SystemName, p.DisplayName, p.Description, p.Fields))
            .ToList();

    public async Task<ServiceResult<NotificationChannelFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var channel = await _repository.GetChannelAsync(id, cancellationToken);
        if (channel is null)
            return ServiceResult<NotificationChannelFormDto>.NotFound(NotFoundMessage);

        var dto = new NotificationChannelFormDto
        {
            Id = channel.Id,
            Name = channel.Name,
            ProviderSystemName = channel.ProviderSystemName,
            MinimumSeverity = channel.MinimumSeverity,
            IsEnabled = channel.IsEnabled
        };

        var stored = TryReadSettings(channel);
        var provider = _registry.Find(channel.ProviderSystemName);
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

        return ServiceResult<NotificationChannelFormDto>.Success(dto);
    }

    public async Task<ServiceResult<Guid>> CreateAsync(NotificationChannelFormDto dto, CancellationToken cancellationToken = default)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var provider = _registry.Find(dto.ProviderSystemName);
        if (provider is null)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.ProviderSystemName), "Kanal türü bulunamadı veya eklentisi etkin değil.");

        if (await _repository.ChannelNameExistsAsync(dto.Name, null, cancellationToken))
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Name), "Bu isimde bir kanal zaten kayıtlı.");

        var (settings, errors) = BuildSettings(provider, dto.Settings, null);
        if (errors.Count > 0)
            return ServiceResult<Guid>.ValidationFailure(errors);

        var channel = new NotificationChannel
        {
            Name = dto.Name,
            ProviderSystemName = provider.SystemName,
            EncryptedSettings = _secretProtector.Protect(NotificationSettingsSerializer.Serialize(settings)),
            MinimumSeverity = dto.MinimumSeverity,
            IsEnabled = dto.IsEnabled,
            CreatedAt = UtcNow,
            CreatedBy = _currentUser.UserName
        };

        await _repository.AddChannelAsync(channel, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bildirim kanalı eklendi. ChannelId: {ChannelId}, Provider: {Provider}", channel.Id, channel.ProviderSystemName);
        await AuditAsync(AuditActions.NotificationChannelCreate, channel, $"Tür: {provider.DisplayName} | En düşük önem: {channel.MinimumSeverity}", true, cancellationToken);
        return ServiceResult<Guid>.Success(channel.Id, "Bildirim kanalı eklendi.");
    }

    public async Task<ServiceResult> UpdateAsync(NotificationChannelFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var channel = await _repository.GetChannelAsync(id, cancellationToken);
        if (channel is null)
            return ServiceResult.NotFound(NotFoundMessage);

        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.ProviderSystemName = channel.ProviderSystemName;
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var provider = _registry.Find(channel.ProviderSystemName);
        if (provider is null)
            return ServiceResult.Failure("Kanalın eklentisi etkin değil; önce eklentiyi etkinleştirin.");

        if (await _repository.ChannelNameExistsAsync(dto.Name, channel.Id, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), "Bu isimde bir kanal zaten kayıtlı.");

        var stored = TryReadSettings(channel);
        if (stored is null && provider.Fields.Any(f => f.IsSecret && string.IsNullOrEmpty(dto.Settings.GetValueOrDefault(f.Key))))
            return ServiceResult.Failure("Kayıtlı ayarlar çözülemedi; gizli alanları yeniden girin.");

        var (settings, errors) = BuildSettings(provider, dto.Settings, stored);
        if (errors.Count > 0)
            return ServiceResult.ValidationFailure(errors);

        var changes = new List<string>();
        if (channel.Name != dto.Name) changes.Add($"Ad: {channel.Name} -> {dto.Name}");
        if (channel.MinimumSeverity != dto.MinimumSeverity) changes.Add($"En düşük önem: {channel.MinimumSeverity} -> {dto.MinimumSeverity}");
        if (channel.IsEnabled != dto.IsEnabled) changes.Add(dto.IsEnabled ? "Etkinleştirildi" : "Devre dışı bırakıldı");
        if (stored is null || !SameSettings(stored, settings)) changes.Add("Ayarlar güncellendi");

        channel.Name = dto.Name;
        channel.MinimumSeverity = dto.MinimumSeverity;
        channel.IsEnabled = dto.IsEnabled;
        channel.EncryptedSettings = _secretProtector.Protect(NotificationSettingsSerializer.Serialize(settings));
        channel.UpdatedAt = UtcNow;
        channel.UpdatedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.NotificationChannelUpdate, channel, changes.Count == 0 ? "Değişiklik yok" : string.Join(" | ", changes), true, cancellationToken);
        return ServiceResult.Success("Bildirim kanalı güncellendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var channel = await _repository.GetChannelAsync(id, cancellationToken);
        if (channel is null)
            return ServiceResult.NotFound(NotFoundMessage);

        channel.IsDeleted = true;
        channel.DeletedAt = UtcNow;
        channel.DeletedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.NotificationChannelDelete, channel, null, true, cancellationToken);
        return ServiceResult.Success("Bildirim kanalı silindi; bağlı kurallar bu kanala bildirim göndermeyecek.");
    }

    public async Task<ServiceResult> SendTestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var channel = await _repository.GetChannelAsync(id, cancellationToken);
        if (channel is null)
            return ServiceResult.NotFound(NotFoundMessage);

        var message = AlertMessageBuilder.BuildTest(channel.Name, UtcNow, _options.PublicBaseUrl);
        var result = await _dispatcher.SendAsync(channel, message, null, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.NotificationChannelTest, channel, result.Message, result.IsSuccess, cancellationToken);
        return result.IsSuccess
            ? ServiceResult.Success($"Test bildirimi {channel.Name} kanalına gönderildi.")
            : ServiceResult.Failure(result.Message ?? "Test bildirimi gönderilemedi.");
    }

    public async Task<IReadOnlyList<NotificationDeliveryDto>> GetRecentDeliveriesAsync(int count, CancellationToken cancellationToken = default)
    {
        var deliveries = await _repository.GetRecentDeliveriesAsync(Math.Clamp(count, 1, 100), cancellationToken);
        return deliveries.Select(d => new NotificationDeliveryDto(d.ChannelName, d.Kind, d.IsSuccess, d.Message, d.SentAt)).ToList();
    }

    /// <summary>Yalnızca sağlayıcının tanımladığı alanlar saklanır; boş gizli alan kayıtlı değeri korur.</summary>
    public static (Dictionary<string, string> Settings, List<ServiceError> Errors) BuildSettings(
        INotificationChannelProvider provider,
        IReadOnlyDictionary<string, string?> input,
        IReadOnlyDictionary<string, string>? stored)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<ServiceError>();

        foreach (var field in provider.Fields)
        {
            var key = $"{SettingsPrefix}[{field.Key}]";
            var value = input.GetValueOrDefault(field.Key)?.Trim();
            if (string.IsNullOrEmpty(value) && field.IsSecret && stored is not null && stored.TryGetValue(field.Key, out var existing))
                value = existing;
            if (string.IsNullOrEmpty(value))
                value = field.IsSecret ? null : field.DefaultValue;

            if (string.IsNullOrEmpty(value))
            {
                if (field.IsRequired)
                    errors.Add(new ServiceError(key, $"{field.Label} zorunludur."));
                continue;
            }

            if (value.Length > field.MaxLength)
            {
                errors.Add(new ServiceError(key, $"{field.Label} en fazla {field.MaxLength} karakter olabilir."));
                continue;
            }

            var formatError = field.Type switch
            {
                NotificationFieldType.Number when !int.TryParse(value, out _) => $"{field.Label} bir sayı olmalıdır.",
                NotificationFieldType.Select when field.Options is { Count: > 0 } options && options.All(o => o.Value != value) => $"{field.Label} için geçerli bir seçenek seçin.",
                NotificationFieldType.Url when !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") => $"{field.Label} http(s):// ile başlayan geçerli bir adres olmalıdır.",
                _ => null
            };
            if (formatError is not null)
            {
                errors.Add(new ServiceError(key, formatError));
                continue;
            }

            settings[field.Key] = value;
        }

        if (errors.Count == 0)
        {
            errors.AddRange(provider.Validate(settings).Select(e =>
                new ServiceError(string.IsNullOrEmpty(e.PropertyName) ? string.Empty : $"{SettingsPrefix}[{e.PropertyName}]", e.Message)));
        }

        return (settings, errors);
    }

    private Dictionary<string, string>? TryReadSettings(NotificationChannel channel)
    {
        try
        {
            return NotificationSettingsSerializer.Deserialize(_secretProtector.Unprotect(channel.EncryptedSettings));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            _logger.LogWarning(ex, "Bildirim kanalı ayarları çözülemedi. ChannelId: {ChannelId}", channel.Id);
            return null;
        }
    }

    private static bool SameSettings(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private Task AuditAsync(string action, NotificationChannel channel, string? details, bool isSuccess, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.NotificationChannel, channel.Id.ToString(), channel.Name, details, isSuccess), cancellationToken);
}
