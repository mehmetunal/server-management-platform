namespace ServerManager.Domain.Entities;

public class UptimeCheckResult
{
    public long Id { get; set; }

    public Guid CheckId { get; set; }

    public DateTime CheckedAt { get; set; }

    public bool IsUp { get; set; }

    public int ResponseMs { get; set; }

    public int? StatusCode { get; set; }

    public string? Message { get; set; }
}
