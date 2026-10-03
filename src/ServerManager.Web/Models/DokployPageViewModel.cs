namespace ServerManager.Web.Models;

public sealed class DokployPageViewModel
{
    public required ServerPageViewModel Page { get; init; }

    /// <summary>Kurulum sayfasında izlenecek kurulum; yoksa sihirbaz uyumluluk kontrolüyle başlar.</summary>
    public Guid? InstallationId { get; init; }

    public Guid ServerId => Page.Server.Id;

    public string ServerName => Page.Server.Name;
}
