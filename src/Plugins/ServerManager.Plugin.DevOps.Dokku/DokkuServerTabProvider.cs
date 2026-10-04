using ServerManager.Web.Framework.Servers;

namespace ServerManager.Plugin.DevOps.Dokku;

public sealed class DokkuServerTabProvider : IServerTabProvider
{
    public IEnumerable<ServerTab> GetTabs() =>
    [
        new ServerTab(
            DokkuPlugin.ServerTabKey,
            "Dokku",
            "Dokku uygulamaları",
            "Sunucudaki Dokku uygulamalarını görün, kurun ve yeniden başlatın.",
            DokkuPermissions.View,
            "Dokku",
            Order: 20)
    ];
}
