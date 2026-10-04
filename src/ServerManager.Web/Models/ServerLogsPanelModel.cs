using ServerManager.Application.ServerSystem;

namespace ServerManager.Web.Models;

public sealed class ServerLogsPanelModel
{
    public required LogRequest Request { get; init; }

    public required LogSnapshot Snapshot { get; init; }
}
