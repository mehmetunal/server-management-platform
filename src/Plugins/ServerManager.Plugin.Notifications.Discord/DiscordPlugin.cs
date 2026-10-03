namespace ServerManager.Plugin.Notifications.Discord;

public static class DiscordPlugin
{
    /// <summary>plugin.json içindeki SystemName ile aynı olmalıdır; kanal kaydında sağlayıcı adı olarak saklanır.</summary>
    public const string SystemName = "Notifications.Discord";

    public const string DisplayName = "Discord";

    public const string HttpClientName = "ServerManager.Discord";

    public const string WebhookUrlKey = "WebhookUrl";

    public const string UsernameKey = "Username";
}
