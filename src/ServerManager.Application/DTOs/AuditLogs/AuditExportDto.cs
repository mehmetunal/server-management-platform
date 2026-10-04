namespace ServerManager.Application.DTOs.AuditLogs;

public sealed class AuditExportDto
{
    public IReadOnlyList<AuditLogDto> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public bool Truncated => TotalCount > Items.Count;
}
