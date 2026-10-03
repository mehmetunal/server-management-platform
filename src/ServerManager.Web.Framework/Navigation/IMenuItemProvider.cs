namespace ServerManager.Web.Framework.Navigation;

public interface IMenuItemProvider
{
    IEnumerable<MenuItem> GetItems();
}
