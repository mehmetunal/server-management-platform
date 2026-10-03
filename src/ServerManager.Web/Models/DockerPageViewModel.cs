namespace ServerManager.Web.Models;

public sealed class DockerPageViewModel
{
    public const string OverviewSection = "overview";
    public const string ContainersSection = "containers";
    public const string ImagesSection = "images";
    public const string VolumesSection = "volumes";
    public const string NetworksSection = "networks";

    public required ServerPageViewModel Page { get; init; }

    public required string Section { get; init; }

    public string? Container { get; init; }

    public Guid ServerId => Page.Server.Id;

    public string ServerName => Page.Server.Name;
}
