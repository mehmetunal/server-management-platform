namespace ServerManager.Application.Docker;

public sealed class DockerOptions
{
    public const string SectionName = "Docker";

    public int CommandTimeoutSeconds { get; set; } = 60;

    public int PullTimeoutSeconds { get; set; } = 600;

    public int DefaultLogTail { get; set; } = 200;

    public int MaxLogTail { get; set; } = 5000;

    public int TerminalIdleTimeoutMinutes { get; set; } = 30;

    public int MaxTerminalSessionsPerUser { get; set; } = 3;
}
