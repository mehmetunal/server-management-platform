using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class TerminalSessionLog
{
    public Guid Id { get; set; }

    public Guid ServerId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? IpAddress { get; set; }

    public TerminalSessionKind Kind { get; set; }

    public string? Container { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public string? CloseReason { get; set; }

    public int CommandCount { get; set; }
}
