using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Application.Notifications;
using ServerManager.Domain.Enums;

namespace ServerManager.Plugin.Notifications.Discord;

public sealed class DiscordNotificationProvider : INotificationChannelProvider
{
    public const int MaxTitleLength = 256;
    public const int MaxDescriptionLength = 4096;
    public const string DefaultUsername = "Server Manager";

    private const string WebhookPathPrefix = "/api/webhooks/";

    private static readonly IReadOnlyList<NotificationSettingField> FieldDefinitions =
    [
        new(DiscordPlugin.WebhookUrlKey, "Webhook adresi", NotificationFieldType.Secret,
            Hint: "Kanal ayarları → Entegrasyonlar → Webhook'lar → Webhook URL'sini kopyala. Kaydedildikten sonra gösterilmez.",
            Placeholder: "https://discord.com/api/webhooks/...",
            MaxLength: 300),
        new(DiscordPlugin.UsernameKey, "Görünen ad", NotificationFieldType.Text, IsRequired: false,
            Hint: "Mesajı gönderen adı; boş bırakılırsa \"Server Manager\".",
            DefaultValue: DefaultUsername,
            MaxLength: 80)
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<DiscordOptions> _options;

    public DiscordNotificationProvider(IHttpClientFactory httpClientFactory, IOptions<DiscordOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string SystemName => DiscordPlugin.SystemName;

    public string DisplayName => DiscordPlugin.DisplayName;

    public string Description => "Discord kanalına webhook ile renkli bildirim kartı gönderir.";

    public IReadOnlyList<NotificationSettingField> Fields => FieldDefinitions;

    public IReadOnlyList<ServiceError> Validate(IReadOnlyDictionary<string, string> settings)
    {
        var errors = new List<ServiceError>();
        if (settings.TryGetValue(DiscordPlugin.WebhookUrlKey, out var url) && ValidateWebhook(url) is { } error)
            errors.Add(new ServiceError(DiscordPlugin.WebhookUrlKey, error));

        if (settings.TryGetValue(DiscordPlugin.UsernameKey, out var username)
            && username.Contains("discord", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ServiceError(DiscordPlugin.UsernameKey, "Discord görünen adda \"discord\" kelimesine izin vermez."));
        }

        return errors;
    }

    public string? ValidateWebhook(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "Geçerli bir webhook adresi girin.";

        var options = _options.Value;
        if (uri.Scheme != Uri.UriSchemeHttps && !(options.AllowHttp && uri.Scheme == Uri.UriSchemeHttp))
            return "Webhook adresi https ile başlamalıdır.";

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return "Webhook adresi kullanıcı bilgisi içeremez.";

        var allowed = options.AllowedHosts.Any(h =>
            string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase)
            || string.Equals(h, uri.Authority, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
            return $"Webhook adresi yalnızca şu sunuculara gidebilir: {string.Join(", ", options.AllowedHosts)}.";

        if (!uri.AbsolutePath.StartsWith(WebhookPathPrefix, StringComparison.Ordinal))
            return "Webhook adresi /api/webhooks/ ile devam etmelidir.";

        return null;
    }

    public async Task<ServiceResult> SendAsync(IReadOnlyDictionary<string, string> settings, NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (!settings.TryGetValue(DiscordPlugin.WebhookUrlKey, out var url) || ValidateWebhook(url) is not null)
            return ServiceResult.Failure("Discord webhook adresi eksik veya izin verilmeyen bir adres.");

        var username = settings.TryGetValue(DiscordPlugin.UsernameKey, out var name) && !string.IsNullOrWhiteSpace(name) ? name : DefaultUsername;
        var payload = BuildPayload(message, username);

        try
        {
            var client = _httpClientFactory.CreateClient(DiscordPlugin.HttpClientName);
            using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
                return ServiceResult.Success("Discord mesajı gönderildi.");

            return ServiceResult.Failure(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound => "Discord webhook bulunamadı veya silinmiş.",
                HttpStatusCode.TooManyRequests => "Discord hız sınırına takıldı; biraz sonra tekrar denenecek.",
                HttpStatusCode.BadRequest => "Discord mesajı reddetti (HTTP 400).",
                _ => $"Discord HTTP {(int)response.StatusCode} döndü."
            });
        }
        catch (HttpRequestException)
        {
            return ServiceResult.Failure("Discord'a bağlanılamadı.");
        }
    }

    public static object BuildPayload(NotificationMessage message, string username)
    {
        var footer = message.ServerName is null ? message.SeverityText : $"{message.SeverityText} • {message.ServerName}";
        var embed = new Dictionary<string, object?>
        {
            ["title"] = TextHelper.Truncate(message.Title, MaxTitleLength),
            ["description"] = TextHelper.Truncate(message.Body, MaxDescriptionLength),
            ["color"] = Color(message),
            ["timestamp"] = DateTime.SpecifyKind(message.OccurredAt, DateTimeKind.Utc).ToString("O"),
            ["footer"] = new Dictionary<string, string> { ["text"] = footer }
        };
        if (message.Url is not null)
            embed["url"] = message.Url;

        return new Dictionary<string, object>
        {
            ["username"] = TextHelper.Truncate(username, 80) ?? DefaultUsername,
            ["embeds"] = new[] { embed },
            ["allowed_mentions"] = new Dictionary<string, object> { ["parse"] = Array.Empty<string>() }
        };
    }

    public static int Color(NotificationMessage message) => message.Kind switch
    {
        NotificationKind.Recovery => 0x2F9E44,
        NotificationKind.Test => 0x1C7ED6,
        _ => message.Severity == AlertSeverity.Critical ? 0xE03131 : 0xF08C00
    };
}
