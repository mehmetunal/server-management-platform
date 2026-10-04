using ServerManager.Application.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Servers;

public sealed class ServerFilterDto
{
    public string? Search { get; set; }

    public ServerStatus? Status { get; set; }

    public ServerEnvironment? Environment { get; set; }

    public string? Tag { get; set; }

    public Guid? GroupId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}
