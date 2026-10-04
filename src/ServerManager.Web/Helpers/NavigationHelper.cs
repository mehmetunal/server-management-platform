using Microsoft.AspNetCore.Mvc.Rendering;

namespace ServerManager.Web.Helpers;

public static class NavigationHelper
{
    public static string ActiveClass(ViewContext viewContext, string controller) =>
        string.Equals(viewContext.RouteData.Values["controller"] as string, controller, StringComparison.OrdinalIgnoreCase)
            ? "is-active"
            : string.Empty;

    public static string ActiveActionClass(ViewContext viewContext, string controller, string action) =>
        string.Equals(viewContext.RouteData.Values["controller"] as string, controller, StringComparison.OrdinalIgnoreCase)
        && string.Equals(viewContext.RouteData.Values["action"] as string, action, StringComparison.OrdinalIgnoreCase)
            ? "is-active"
            : string.Empty;

    public static string ActiveClass(ViewContext viewContext, params string[] controllers) =>
        controllers.Contains(viewContext.RouteData.Values["controller"] as string, StringComparer.OrdinalIgnoreCase)
            ? "is-active"
            : string.Empty;
}
