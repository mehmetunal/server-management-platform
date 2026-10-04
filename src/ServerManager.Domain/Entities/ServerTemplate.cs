using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class ServerTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ServerTemplateKind Kind { get; set; } = ServerTemplateKind.Script;

    public string Content { get; set; } = string.Empty;

    public bool RequiresSudo { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
