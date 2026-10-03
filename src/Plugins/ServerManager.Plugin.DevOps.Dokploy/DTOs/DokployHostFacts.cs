namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

/// <summary>Uyumluluk kontrolü için sunucudan toplanan ham bilgiler; değerlendirme Application katmanında yapılır.</summary>
public sealed class DokployHostFacts
{
    public string? OsId { get; init; }

    public string? OsVersion { get; init; }

    public string? OsName { get; init; }

    public string? Kernel { get; init; }

    public string? Architecture { get; init; }

    public int? UserId { get; init; }

    /// <summary>"none", "docker" veya "lxc".</summary>
    public string? ContainerKind { get; init; }

    public long? MemoryKb { get; init; }

    public long? DiskAvailableKb { get; init; }

    public bool CurlAvailable { get; init; }

    public bool BashAvailable { get; init; }

    public bool UseSudo { get; init; }

    /// <summary>Kullanıcı root değilse sudo denemesinin sonucu; denenmediyse null.</summary>
    public bool? SudoWorks { get; init; }

    public string? SudoError { get; init; }

    public bool DockerInstalled { get; init; }

    public string? DockerVersion { get; init; }

    public string? DockerError { get; init; }

    public string? SwarmState { get; init; }

    public bool DokployServiceExists { get; init; }

    /// <summary>Dinlenen portlar; port aracı (ss/netstat) yoksa null.</summary>
    public IReadOnlyList<int>? ListeningPorts { get; init; }

    public bool ScriptReachable { get; init; }

    public string? ScriptError { get; init; }

    public bool RegistryReachable { get; init; }

    public string? RegistryError { get; init; }
}
