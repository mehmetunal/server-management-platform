using ServerManager.Application.Common;

namespace ServerManager.Application.DTOs.AuditLogs;

public sealed class AuditLogFilterDto
{
    public string? Search { get; set; }

    public string? Action { get; set; }

    public bool? IsSuccess { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}
