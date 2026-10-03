namespace ServerManager.Web.Framework.Servers;

public interface IServerTabProvider
{
    IEnumerable<ServerTab> GetTabs();
}
