using ServerManager.Application.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Uptime;

public sealed class UptimeFilterDto
{
    public string? Search { get; set; }

    public UptimeStatus? Status { get; set; }

    public Guid? ServerId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}
