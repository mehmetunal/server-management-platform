namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerContainerDto
{
    public string Id { get; init; } = string.Empty;

    public string ShortId => Id.Length > 12 ? Id[..12] : Id;

    public string Name { get; init; } = string.Empty;

    public string Image { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Health { get; init; }

    public string Ports { get; init; } = string.Empty;

    public IReadOnlyList<string> Networks { get; init; } = [];

    public string? ComposeProject { get; init; }

    public int? ExitCode { get; init; }

    public int RestartCount { get; init; }

    public DateTime? CreatedAt { get; init; }

    public DateTime? StartedAt { get; init; }
}
