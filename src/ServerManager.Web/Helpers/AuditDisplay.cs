using Microsoft.AspNetCore.Mvc.Rendering;
using ServerManager.Application.Auditing;

namespace ServerManager.Web.Helpers;

public static class AuditDisplay
{
    public static IEnumerable<SelectListItem> ActionOptions(AuditActionCatalog catalog, string? selected) =>
        catalog.DisplayNames.Select(kv => new SelectListItem(kv.Value, kv.Key, kv.Key == selected));
}
