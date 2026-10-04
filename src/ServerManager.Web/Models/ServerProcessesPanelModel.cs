using ServerManager.Application.ServerSystem;

namespace ServerManager.Web.Models;

public sealed class ServerProcessesPanelModel
{
    public required Guid ServerId { get; init; }

    public required ProcessList Processes { get; init; }
}
