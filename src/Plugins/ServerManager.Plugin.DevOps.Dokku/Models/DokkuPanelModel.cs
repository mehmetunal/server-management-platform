namespace ServerManager.Plugin.DevOps.Dokku.Models;

public sealed class DokkuPanelModel<T>
{
    public required Guid ServerId { get; init; }

    public required T Data { get; init; }
}
