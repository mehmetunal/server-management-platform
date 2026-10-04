namespace ServerManager.Application.Security;

public sealed class DockerFacts
{
    public bool Installed { get; init; }

    /// <summary>docker ps çalıştırılabildiyse true; yetki yoksa false.</summary>
    public bool Accessible { get; init; }

    /// <summary>dockerd'nin dinlediği -H / hosts değerleri (ör. unix:///var/run/docker.sock, tcp://0.0.0.0:2375).</summary>
    public IReadOnlyList<string> DaemonHosts { get; init; } = [];

    public bool TlsVerify { get; init; }

    public IReadOnlyList<PublishedPort> PublishedPorts { get; init; } = [];
}
