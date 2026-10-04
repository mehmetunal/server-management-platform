namespace ServerManager.Plugin.Cloud.Vultr;

public sealed class VultrOptions
{
    public const string SectionName = "Cloud:Vultr";

    public const string DefaultApiUrl = "https://api.vultr.com/v2/";

    /// <summary>Yalnızca test veya vekil sunucu için değiştirilir.</summary>
    public string ApiUrl { get; set; } = DefaultApiUrl;
}
