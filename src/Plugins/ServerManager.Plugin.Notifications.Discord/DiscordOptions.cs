namespace ServerManager.Plugin.Notifications.Discord;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>Webhook adresinin gidebileceği sunucular; "host" veya "host:port" biçiminde.</summary>
    public string[] AllowedHosts { get; set; } = ["discord.com", "discordapp.com", "ptb.discord.com", "canary.discord.com"];

    /// <summary>Yalnızca yerel test sunucusu için; üretimde https zorunludur.</summary>
    public bool AllowHttp { get; set; }
}
