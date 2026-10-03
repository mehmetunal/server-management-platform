namespace ServerManager.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? TargetName { get; set; }

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool IsSuccess { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
