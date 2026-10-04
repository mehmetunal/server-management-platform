using ServerManager.Application.ServerSystem;

namespace ServerManager.Web.Models;

public sealed class ServerServicesPanelModel
{
    public required Guid ServerId { get; init; }

    public required ServiceList Services { get; init; }
}
