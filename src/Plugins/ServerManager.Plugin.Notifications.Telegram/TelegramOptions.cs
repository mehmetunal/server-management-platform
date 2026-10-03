namespace ServerManager.Plugin.Notifications.Telegram;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public const string DefaultApiUrl = "https://api.telegram.org";

    /// <summary>Bot API adresi; yalnızca kendi barındırdığınız Bot API sunucusu veya test için değiştirilir.</summary>
    public string ApiUrl { get; set; } = DefaultApiUrl;
}
