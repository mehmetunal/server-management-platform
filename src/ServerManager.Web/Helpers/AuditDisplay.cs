using Microsoft.AspNetCore.Mvc.Rendering;
using ServerManager.Application.Auditing;

namespace ServerManager.Web.Helpers;

public static class AuditDisplay
{
    public static string ActionText(string action) =>
        AuditActions.DisplayNames.TryGetValue(action, out var text) ? text : action;

    public static IEnumerable<SelectListItem> ActionOptions(string? selected) =>
        AuditActions.DisplayNames.Select(kv => new SelectListItem(kv.Value, kv.Key, kv.Key == selected));
}
