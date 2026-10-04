namespace ServerManager.Plugin.Cloud.Scaleway;

public sealed class ScalewayOptions
{
    public const string SectionName = "Cloud:Scaleway";

    public const string DefaultApiUrl = "https://api.scaleway.com/";

    /// <summary>Yalnızca test veya vekil sunucu için değiştirilir.</summary>
    public string ApiUrl { get; set; } = DefaultApiUrl;
}
