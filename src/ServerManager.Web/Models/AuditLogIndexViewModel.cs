using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;

namespace ServerManager.Web.Models;

public sealed class AuditLogIndexViewModel
{
    public const string ActionQueryKey = "auditAction";

    public required PagedResult<AuditLogDto> Logs { get; init; }

    public required AuditLogFilterDto Filter { get; init; }

    public PagerModel ToPager() => new()
    {
        Page = Logs.Page,
        TotalPages = Logs.TotalPages,
        TotalCount = Logs.TotalCount,
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = Filter.Search,
            [ActionQueryKey] = Filter.Action,
            ["isSuccess"] = Filter.IsSuccess?.ToString().ToLowerInvariant(),
            ["from"] = Filter.From?.ToString("yyyy-MM-dd"),
            ["to"] = Filter.To?.ToString("yyyy-MM-dd")
        }
    };
}
