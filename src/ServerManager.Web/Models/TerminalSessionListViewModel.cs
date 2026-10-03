using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Terminal;

namespace ServerManager.Web.Models;

public sealed class TerminalSessionListViewModel
{
    public required Guid ServerId { get; init; }

    public required PagedResult<TerminalSessionDto> Sessions { get; init; }

    /// <summary>false ise kullanıcı yalnızca kendi oturumlarını görür.</summary>
    public bool ShowsAllUsers { get; init; }

    public PagerModel ToPager() => new()
    {
        Page = Sessions.Page,
        TotalPages = Sessions.TotalPages,
        TotalCount = Sessions.TotalCount,
        Action = "Sessions",
        RouteValues = new Dictionary<string, string?> { ["id"] = ServerId.ToString() }
    };
}
