namespace ServerManager.Application.Security;

/// <summary>Sunucudan salt okunur komutlarla toplanan ham bilgiler; değerlendirme <see cref="SecurityAnalyzer"/> ile yapılır.</summary>
public sealed class SecurityFacts
{
    public bool IsRoot { get; init; }

    public string? OperatingSystem { get; init; }

    public string? Kernel { get; init; }

    public SshFacts Ssh { get; init; } = new();

    /// <summary>ss veya netstat bulunamadıysa false.</summary>
    public bool PortsAvailable { get; init; }

    public IReadOnlyList<ListeningPort> Ports { get; init; } = [];

    public FirewallFacts Firewall { get; init; } = new();

    public FailedLoginFacts FailedLogins { get; init; } = new();

    public DockerFacts Docker { get; init; } = new();

    public UpdateFacts Updates { get; init; } = new();

    public DiskFacts Disk { get; init; } = new();

    public UserFacts Users { get; init; } = new();
}
