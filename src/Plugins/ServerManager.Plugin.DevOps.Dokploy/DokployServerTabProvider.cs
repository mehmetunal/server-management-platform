using ServerManager.Web.Framework.Servers;

namespace ServerManager.Plugin.DevOps.Dokploy;

public sealed class DokployServerTabProvider : IServerTabProvider
{
    public IEnumerable<ServerTab> GetTabs() =>
    [
        new ServerTab(
            DokployPlugin.ServerTabKey,
            "Dokploy",
            "Dokploy kurulumu, durumu ve projeleri",
            "Sunucudaki Dokploy kurulumunun durumunu izleyin, sağlık kontrolü yapın ve projelerini görün.",
            DokployPermissions.View,
            "Dokploy",
            Order: 10)
    ];
}
