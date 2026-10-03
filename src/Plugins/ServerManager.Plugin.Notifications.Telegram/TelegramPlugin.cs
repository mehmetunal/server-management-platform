namespace ServerManager.Plugin.Notifications.Telegram;

public static class TelegramPlugin
{
    /// <summary>plugin.json içindeki SystemName ile aynı olmalıdır; kanal kaydında sağlayıcı adı olarak saklanır.</summary>
    public const string SystemName = "Notifications.Telegram";

    public const string DisplayName = "Telegram";

    public const string HttpClientName = "ServerManager.Telegram";

    public const string BotTokenKey = "BotToken";

    public const string ChatIdKey = "ChatId";
}
