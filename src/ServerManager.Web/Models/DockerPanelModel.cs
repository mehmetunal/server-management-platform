namespace ServerManager.Web.Models;

public sealed class DockerPanelModel<T>
{
    public required Guid ServerId { get; init; }

    public required T Data { get; init; }
}
