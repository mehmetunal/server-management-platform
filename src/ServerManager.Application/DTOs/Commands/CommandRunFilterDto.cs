using ServerManager.Application.Common;

namespace ServerManager.Application.DTOs.Commands;

public sealed class CommandRunFilterDto
{
    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}
