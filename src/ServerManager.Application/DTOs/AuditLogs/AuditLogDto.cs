namespace ServerManager.Application.DTOs.AuditLogs;

public sealed class AuditLogDto
{
    public long Id { get; init; }

    public string? UserId { get; init; }

    public string? UserName { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public string? TargetName { get; init; }

    public string? Details { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public bool IsSuccess { get; init; }

    public DateTime CreatedAt { get; init; }

    /// <summary>Bütünlük zinciri imzası; zincir öncesi eski kayıtlarda boştur.</summary>
    public string? ChainHash { get; init; }
}
