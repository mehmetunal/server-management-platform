namespace ServerManager.Infrastructure.Ssh;

public sealed class SshOptions
{
    public const string SectionName = "Ssh";

    public int ConnectionTimeoutSeconds { get; set; } = 10;

    public int CommandTimeoutSeconds { get; set; } = 10;
}
