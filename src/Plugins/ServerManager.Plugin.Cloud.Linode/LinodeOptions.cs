namespace ServerManager.Plugin.Cloud.Linode;

public sealed class LinodeOptions
{
    public const string SectionName = "Cloud:Linode";

    public const string DefaultApiUrl = "https://api.linode.com/v4/";

    /// <summary>Yalnızca test veya vekil sunucu için değiştirilir.</summary>
    public string ApiUrl { get; set; } = DefaultApiUrl;
}
