namespace ServerManager.Plugin.DevOps.Dokploy.Models;

public sealed class DokployPanelModel<T>
{
    public required Guid ServerId { get; init; }

    public required T Data { get; init; }
}
