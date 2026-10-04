using ServerManager.Application.Common;

namespace ServerManager.Application.DTOs.AuditLogs;

public sealed class AuditLogFilterDto
{
    public string? Search { get; set; }

    public string? Action { get; set; }

    public bool? IsSuccess { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    /// <summary>Kullanıcı adında geçen metin.</summary>
    public string? User { get; set; }

    /// <summary>IP adresinin başı (ör. 10.0. veya tam adres).</summary>
    public string? Ip { get; set; }

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}
