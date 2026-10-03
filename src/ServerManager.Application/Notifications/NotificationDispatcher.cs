using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Notifications;

public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationChannelRegistry _registry;
    private readonly ISecretProtector _secretProtector;
    private readonly IAlertRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly AlertingOptions _options;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationChannelRegistry registry,
        ISecretProtector secretProtector,
        IAlertRepository repository,
        TimeProvider timeProvider,
        IOptions<AlertingOptions> options,
        ILogger<NotificationDispatcher> logger)
    {
        _registry = registry;
        _secretProtector = secretProtector;
        _repository = repository;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult> SendAsync(NotificationChannel channel, NotificationMessage message, Guid? alertEventId, CancellationToken cancellationToken = default)
    {
        var result = await SendCoreAsync(channel, message, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _repository.AddDeliveryAsync(new NotificationDelivery
        {
            ChannelId = channel.Id,
            ChannelName = channel.Name,
            AlertEventId = alertEventId,
            Kind = message.Kind,
            IsSuccess = result.IsSuccess,
            Message = TextHelper.Truncate(result.Message, 500),
            SentAt = now
        }, cancellationToken);

        if (result.IsSuccess)
        {
            channel.LastSentAt = now;
            channel.LastError = null;
        }
        else
        {
            channel.LastError = TextHelper.Truncate(result.Message, 500);
            _logger.LogWarning("Bildirim gönderilemedi. ChannelId: {ChannelId}, Kind: {Kind}, Reason: {Reason}", channel.Id, message.Kind, result.Message);
        }

        return result;
    }

    private async Task<ServiceResult> SendCoreAsync(NotificationChannel channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var provider = _registry.Find(channel.ProviderSystemName);
        if (provider is null)
            return ServiceResult.Failure("Kanalın eklentisi kurulu veya etkin değil.");

        Dictionary<string, string> settings;
        try
        {
            settings = NotificationSettingsSerializer.Deserialize(_secretProtector.Unprotect(channel.EncryptedSettings));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            _logger.LogError(ex, "Bildirim kanalı ayarları çözülemedi. ChannelId: {ChannelId}", channel.Id);
            return ServiceResult.Failure("Kanal ayarları çözülemedi. Master key değişmiş olabilir; kanalı yeniden kaydedin.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.NotificationTimeoutSeconds, 2, 120)));
        try
        {
            return await provider.SendAsync(settings, message, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceResult.Failure("Bildirim zaman aşımına uğradı.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Bildirim sağlayıcısı hata verdi. ChannelId: {ChannelId}, Provider: {Provider}", channel.Id, channel.ProviderSystemName);
            return ServiceResult.Failure("Bildirim gönderilemedi: beklenmeyen hata.");
        }
    }
}
