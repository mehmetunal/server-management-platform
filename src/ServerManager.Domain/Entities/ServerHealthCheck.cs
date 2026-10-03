namespace ServerManager.Domain.Entities;

public class ServerHealthCheck
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    public bool IsSuccess { get; set; }

    public long ResponseTimeMs { get; set; }

    public string? Message { get; set; }
}
