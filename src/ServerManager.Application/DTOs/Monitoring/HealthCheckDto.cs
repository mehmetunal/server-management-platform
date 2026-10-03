namespace ServerManager.Application.DTOs.Monitoring;

public sealed class HealthCheckDto
{
    public DateTime CheckedAt { get; init; }

    public bool IsSuccess { get; init; }

    public long ResponseTimeMs { get; init; }

    public string? Message { get; init; }
}
