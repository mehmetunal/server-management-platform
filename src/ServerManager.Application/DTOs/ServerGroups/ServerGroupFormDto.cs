namespace ServerManager.Application.DTOs.ServerGroups;

public sealed class ServerGroupFormDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Color { get; set; } = ServerGroupColors.Default;

    public List<Guid> ServerIds { get; set; } = [];
}
