using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class CommandRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Command { get; set; } = string.Empty;

    public Guid? TemplateId { get; set; }

    public string? TemplateName { get; set; }

    public bool UseSudo { get; set; }

    public int TimeoutSeconds { get; set; }

    public CommandRunStatus Status { get; set; } = CommandRunStatus.Running;

    public int TargetCount { get; set; }

    public int SucceededCount { get; set; }

    public int FailedCount { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public ICollection<CommandRunTarget> Targets { get; set; } = new List<CommandRunTarget>();
}
