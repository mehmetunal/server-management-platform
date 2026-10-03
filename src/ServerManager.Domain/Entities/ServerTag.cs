namespace ServerManager.Domain.Entities;

public class ServerTag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Server? Server { get; set; }
}
