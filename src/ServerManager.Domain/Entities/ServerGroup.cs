using ServerManager.Domain.Common;

namespace ServerManager.Domain.Entities;

public class ServerGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Color { get; set; } = "slate";

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public ICollection<Server> Servers { get; set; } = new List<Server>();
}
