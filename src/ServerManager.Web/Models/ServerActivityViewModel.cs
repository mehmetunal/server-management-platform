using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class ServerActivityViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public required AuditLogIndexViewModel List { get; init; }
}
