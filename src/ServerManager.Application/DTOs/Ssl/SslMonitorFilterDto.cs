using ServerManager.Application.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Ssl;

public sealed class SslMonitorFilterDto
{
    public string? Search { get; set; }

    public SslCertificateStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}
