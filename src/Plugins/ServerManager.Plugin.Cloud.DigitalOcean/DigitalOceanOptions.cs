namespace ServerManager.Plugin.Cloud.DigitalOcean;

public sealed class DigitalOceanOptions
{
    public const string SectionName = "Cloud:DigitalOcean";

    public const string DefaultApiUrl = "https://api.digitalocean.com/v2/";

    /// <summary>Yalnızca test veya vekil sunucu için değiştirilir.</summary>
    public string ApiUrl { get; set; } = DefaultApiUrl;
}
