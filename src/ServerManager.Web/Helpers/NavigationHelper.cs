using Microsoft.AspNetCore.Mvc.Rendering;

namespace ServerManager.Web.Helpers;

public static class NavigationHelper
{
    public static string ActiveClass(ViewContext viewContext, string controller) =>
        string.Equals(viewContext.RouteData.Values["controller"] as string, controller, StringComparison.OrdinalIgnoreCase)
            ? "is-active"
            : string.Empty;
}
