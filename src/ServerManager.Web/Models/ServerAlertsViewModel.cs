using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class ServerAlertsViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public required AlertIndexViewModel List { get; init; }
}
