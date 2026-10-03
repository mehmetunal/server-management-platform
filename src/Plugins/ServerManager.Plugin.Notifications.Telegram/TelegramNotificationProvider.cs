using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Application.Notifications;

namespace ServerManager.Plugin.Notifications.Telegram;

public sealed partial class TelegramNotificationProvider : INotificationChannelProvider
{
    public const int MaxMessageLength = 4096;

    private static readonly IReadOnlyList<NotificationSettingField> FieldDefinitions =
    [
        new(TelegramPlugin.BotTokenKey, "Bot anahtarı", NotificationFieldType.Secret,
            Hint: "@BotFather ile oluşturduğunuz botun anahtarı (123456:ABC...). Kaydedildikten sonra gösterilmez.",
            MaxLength: 100),
        new(TelegramPlugin.ChatIdKey, "Sohbet kimliği", NotificationFieldType.Text,
            Hint: "Kişi/grup için sayısal kimlik (gruplarda -100... ile başlar) veya herkese açık kanal için @kanaladi. Botun sohbete eklenmiş olması gerekir.",
            Placeholder: "-1001234567890",
            MaxLength: 64)
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<TelegramOptions> _options;

    public TelegramNotificationProvider(IHttpClientFactory httpClientFactory, IOptions<TelegramOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string SystemName => TelegramPlugin.SystemName;

    public string DisplayName => TelegramPlugin.DisplayName;

    public string Description => "Telegram botu ile kişiye, gruba veya kanala mesaj gönderir.";

    public IReadOnlyList<NotificationSettingField> Fields => FieldDefinitions;

    public IReadOnlyList<ServiceError> Validate(IReadOnlyDictionary<string, string> settings)
    {
        var errors = new List<ServiceError>();
        if (settings.TryGetValue(TelegramPlugin.BotTokenKey, out var token) && !BotTokenPattern().IsMatch(token))
            errors.Add(new ServiceError(TelegramPlugin.BotTokenKey, "Bot anahtarı '123456789:ABC...' biçiminde olmalıdır."));

        if (settings.TryGetValue(TelegramPlugin.ChatIdKey, out var chatId) && !ChatIdPattern().IsMatch(chatId))
            errors.Add(new ServiceError(TelegramPlugin.ChatIdKey, "Sohbet kimliği sayısal olmalı veya @kanaladi biçiminde yazılmalıdır."));

        return errors;
    }

    public async Task<ServiceResult> SendAsync(IReadOnlyDictionary<string, string> settings, NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (!settings.TryGetValue(TelegramPlugin.BotTokenKey, out var token) || !settings.TryGetValue(TelegramPlugin.ChatIdKey, out var chatId))
            return ServiceResult.Failure("Telegram ayarları eksik.");

        if (!TryGetApiBase(out var apiBase))
            return ServiceResult.Failure("Telegram:ApiUrl ayarı geçersiz.");

        var payload = new Dictionary<string, object>
        {
            ["chat_id"] = chatId,
            ["text"] = Format(message),
            ["parse_mode"] = "HTML",
            ["disable_web_page_preview"] = true
        };

        try
        {
            var client = _httpClientFactory.CreateClient(TelegramPlugin.HttpClientName);
            // Anahtardaki ':' göreli adreste şema sanılacağı için adres birleştirilerek kurulur.
            using var response = await client.PostAsJsonAsync(new Uri($"{apiBase.AbsoluteUri}bot{token}/sendMessage"), payload, cancellationToken);
            if (response.IsSuccessStatusCode)
                return ServiceResult.Success("Telegram mesajı gönderildi.");

            var description = await ReadDescriptionAsync(response, cancellationToken);
            return ServiceResult.Failure(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.NotFound => "Telegram bot anahtarı geçersiz.",
                HttpStatusCode.TooManyRequests => "Telegram hız sınırına takıldı; biraz sonra tekrar denenecek.",
                _ when description is not null => $"Telegram: {description}",
                _ => $"Telegram HTTP {(int)response.StatusCode} döndü."
            });
        }
        catch (HttpRequestException)
        {
            return ServiceResult.Failure("Telegram API'ye bağlanılamadı.");
        }
    }

    public static string Format(NotificationMessage message)
    {
        var builder = new StringBuilder()
            .Append("<b>").Append(Escape(message.Title)).Append("</b>\n\n")
            .Append(Escape(message.Body));

        if (message.Url is not null)
            builder.Append("\n\n<a href=\"").Append(Escape(message.Url)).Append("\">Panelde aç</a>");

        var text = builder.ToString();
        return text.Length <= MaxMessageLength
            ? text
            : Escape(TextHelper.Truncate($"{message.Title}\n\n{message.Body}", MaxMessageLength - 100) ?? string.Empty);
    }

    // Telegram HTML kipinde yalnızca bu karakterler kaçırılmalıdır; WebUtility Türkçe harfleri de sayısal varlığa çevirir.
    public static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private bool TryGetApiBase(out Uri apiBase)
    {
        var value = (_options.Value.ApiUrl ?? TelegramOptions.DefaultApiUrl).TrimEnd('/') + "/";
        return Uri.TryCreate(value, UriKind.Absolute, out apiBase!) && apiBase.Scheme is "https" or "http";
    }

    private static async Task<string?> ReadDescriptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String
                ? TextHelper.Truncate(description.GetString(), 300)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^\d{5,15}:[A-Za-z0-9_-]{30,64}$")]
    private static partial Regex BotTokenPattern();

    [GeneratedRegex(@"^(-?\d{1,20}|@[A-Za-z][A-Za-z0-9_]{4,31})$")]
    private static partial Regex ChatIdPattern();
}
