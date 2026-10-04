using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class CommandRunTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }

    public Guid ServerId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public CommandTargetStatus Status { get; set; } = CommandTargetStatus.Pending;

    public int? ExitCode { get; set; }

    public string? Output { get; set; }

    public bool OutputTruncated { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public long? DurationMs { get; set; }

    public CommandRun? Run { get; set; }
}
