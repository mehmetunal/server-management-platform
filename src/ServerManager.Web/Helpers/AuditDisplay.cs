using Microsoft.AspNetCore.Mvc.Rendering;
using ServerManager.Application.Auditing;

namespace ServerManager.Web.Helpers;

public static class AuditDisplay
{
    public static IEnumerable<SelectListItem> ActionOptions(AuditActionCatalog catalog, string? selected) =>
        catalog.DisplayNames.Select(kv => new SelectListItem(kv.Value, kv.Key, kv.Key == selected));

    public static IEnumerable<SelectListItem> EntityTypeOptions(IReadOnlyList<string> entityTypes, string? selected)
    {
        var types = entityTypes.ToList();
        if (!string.IsNullOrEmpty(selected) && !types.Contains(selected))
            types.Add(selected);

        return types
            .Select(t => new SelectListItem(AuditEntityTypes.DisplayName(t), t, t == selected))
            .OrderBy(item => item.Text, StringComparer.CurrentCulture);
    }
}
