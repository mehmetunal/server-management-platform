using ServerManager.Web.Framework.Servers;

namespace ServerManager.Plugin.DevOps.Dokku.Models;

public sealed class DokkuPageViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public Guid ServerId => Page.Server.Id;

    public string ServerName => Page.Server.Name;
}
