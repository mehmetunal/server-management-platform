using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Templates;

public sealed class ServerTemplateFormDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ServerTemplateKind Kind { get; set; } = ServerTemplateKind.Script;

    public string Content { get; set; } = string.Empty;

    public bool RequiresSudo { get; set; }
}
