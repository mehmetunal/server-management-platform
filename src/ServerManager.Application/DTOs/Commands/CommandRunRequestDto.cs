using ServerManager.Application.Commands;

namespace ServerManager.Application.DTOs.Commands;

public sealed class CommandRunRequestDto
{
    public string Command { get; set; } = string.Empty;

    public List<Guid> ServerIds { get; set; } = [];

    public Guid? TemplateId { get; set; }

    public bool UseSudo { get; set; }

    public int TimeoutSeconds { get; set; } = CommandRunRules.DefaultTimeoutSeconds;
}
