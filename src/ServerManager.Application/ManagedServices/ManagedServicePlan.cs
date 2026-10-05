using ServerManager.Domain.Enums;

namespace ServerManager.Application.ManagedServices;

/// <summary>
/// Sunucuda servis container'ını kurmak veya yeniden oluşturmak için gereken her şey. Gizli değerler yalnızca
/// <see cref="EnvironmentFile"/> içindedir ve sunucuya stdin ile yazılır; komut satırlarına girmez.
/// </summary>
public sealed class ManagedServicePlan
{
    public required string Slug { get; init; }

    public required string ContainerName { get; init; }

    public required string TemplateKey { get; init; }

    public required string Image { get; init; }

    public required string Tag { get; init; }

    public string ImageReference => $"{Image}:{Tag}";

    /// <summary>Ortam dosyası içeriği (<c>ANAHTAR=değer</c> satırları, LF ile).</summary>
    public required string EnvironmentFile { get; init; }

    public IReadOnlyList<PublishedPort> Ports { get; init; } = [];

    public ManagedServiceVolumeMode VolumeMode { get; init; } = ManagedServiceVolumeMode.NamedVolume;

    /// <summary>Container içindeki veri klasörü; null ise volume bağlanmaz.</summary>
    public string? DataPath { get; init; }

    public string? HostDataPath { get; init; }

    public string? DataOwner { get; init; }

    public string VolumeName => ManagedServiceNames.VolumeName(Slug);

    public int? MemoryLimitMb { get; init; }

    public decimal? CpuLimit { get; init; }

    /// <summary>Katılınacak ağlar; ilki container oluşturulurken verilir (panel ağı), diğerleri sonradan bağlanır.</summary>
    public IReadOnlyList<string> Networks { get; init; } = [ManagedServiceNames.ServicesNetwork];

    /// <summary>Yoksa panelin oluşturacağı ağlar (sm-services, sm-proxy); kullanıcının seçtiği ağlar var olmalıdır.</summary>
    public IReadOnlyList<string> ManagedNetworks { get; init; } = [ManagedServiceNames.ServicesNetwork];

    public IReadOnlyList<string> Command { get; init; } = [];

    public string? HealthCommand { get; init; }

    public string? ReadinessCommand { get; init; }

    public ServiceFirewallPlan Firewall { get; init; } = ServiceFirewallPlan.None(string.Empty);

    public bool RequiresX86 { get; init; }

    /// <summary>Yeniden oluşturma/yükseltme: eski container yeni imaj indirildikten sonra kaldırılır, volume korunur.</summary>
    public bool ReplaceExisting { get; init; }

    /// <summary>Loglarda maskelenecek değerler.</summary>
    public IReadOnlyList<string> Secrets { get; init; } = [];

    public TimeSpan PullTimeout { get; init; } = TimeSpan.FromMinutes(20);

    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan HealthTimeout { get; init; } = TimeSpan.FromMinutes(3);

    public override string ToString() => $"ManagedServicePlan {{ Slug = {Slug}, Image = {ImageReference} }}";
}

/// <summary>Dışarıya açık portları yalnızca izin verilen kaynaklara açan DOCKER-USER kuralları.</summary>
/// <param name="Tag">Kurallara eklenen yorum; listeleme ve silme bununla yapılır.</param>
/// <param name="Ports">Kısıtlanacak sunucu portları (TCP).</param>
/// <param name="AllowedSources">Normalleştirilmiş CIDR listesi; boşsa kural yazılmaz (eski kurallar silinir).</param>
public sealed record ServiceFirewallPlan(string Tag, IReadOnlyList<int> Ports, IReadOnlyList<string> AllowedSources)
{
    public bool HasRules => Ports.Count > 0 && AllowedSources.Count > 0;

    public static ServiceFirewallPlan None(string tag) => new(tag, [], []);
}

public sealed class ManagedServiceRemovalPlan
{
    public required string Slug { get; init; }

    public required string ContainerName { get; init; }

    public bool RemoveData { get; init; }

    public ManagedServiceVolumeMode VolumeMode { get; init; } = ManagedServiceVolumeMode.NamedVolume;

    public string? HostDataPath { get; init; }

    public string VolumeName => ManagedServiceNames.VolumeName(Slug);

    public string FirewallTag => ManagedServiceNames.FirewallTag(Slug);

    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromMinutes(2);
}

/// <summary>Sunucunun servis kurulumuna uygunluğu (Docker, mimari, diğer PaaS kurulumları).</summary>
public sealed class ServiceHostProbe
{
    public bool DockerInstalled { get; init; }

    public bool DockerRunning { get; init; }

    public string? DockerVersion { get; init; }

    /// <summary><c>uname -m</c> çıktısı (x86_64, aarch64 …).</summary>
    public string? Architecture { get; init; }

    public bool DokployDetected { get; init; }

    public bool DokkuDetected { get; init; }

    public bool IsX86 => Architecture is "x86_64" or "amd64";
}

/// <summary>Servis container'ının anlık durumu.</summary>
public sealed class ServiceRuntimeState
{
    public bool Exists { get; init; }

    /// <summary>running, exited, restarting, created …</summary>
    public string? State { get; init; }

    /// <summary>healthy, unhealthy, starting veya sağlık kontrolü yoksa null.</summary>
    public string? Health { get; init; }

    public DateTime? StartedAt { get; init; }

    public int RestartCount { get; init; }

    public string? Image { get; init; }

    /// <summary>Sunucuda bu servise ait güvenlik duvarı kuralı sayısı (IPv4 + IPv6).</summary>
    public int FirewallRuleCount { get; init; }

    public IReadOnlyList<string> Networks { get; init; } = [];
}
