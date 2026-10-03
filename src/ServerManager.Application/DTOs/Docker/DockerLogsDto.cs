namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerLogsDto
{
    public string Container { get; init; } = string.Empty;

    public IReadOnlyList<DockerLogLineDto> Lines { get; init; } = [];

    public bool Truncated { get; init; }
}
