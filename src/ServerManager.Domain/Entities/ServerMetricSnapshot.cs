namespace ServerManager.Domain.Entities;

public class ServerMetricSnapshot
{
    public Guid ServerId { get; set; }

    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    public string SnapshotJson { get; set; } = "{}";
}
