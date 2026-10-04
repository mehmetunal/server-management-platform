namespace ServerManager.Plugin.Cloud.Hetzner;

public sealed class HetznerOptions
{
    public const string SectionName = "Cloud:Hetzner";

    public const string DefaultApiUrl = "https://api.hetzner.cloud/v1/";

    /// <summary>Yalnızca test veya vekil sunucu için değiştirilir.</summary>
    public string ApiUrl { get; set; } = DefaultApiUrl;
}
