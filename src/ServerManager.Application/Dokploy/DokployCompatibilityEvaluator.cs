using System.Globalization;
using ServerManager.Application.DTOs.Dokploy;

namespace ServerManager.Application.Dokploy;

/// <summary>
/// Sunucudan toplanan bilgileri resmi kurulum betiğinin koşullarına göre değerlendirir.
/// Betiğin kendisinin reddedeceği veya sunucuyu bozacağı durumlar (container, dolu port, mevcut swarm) engelleyicidir.
/// </summary>
public static class DokployCompatibilityEvaluator
{
    private static readonly HashSet<string> SupportedDistributions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ubuntu", "debian", "fedora", "centos", "rhel", "rocky", "almalinux", "ol", "raspbian"
    };

    private static readonly HashSet<string> SupportedArchitectures = new(StringComparer.OrdinalIgnoreCase)
    {
        "x86_64", "amd64", "aarch64", "arm64"
    };

    public static DokployCompatibilityReportDto Evaluate(DokployHostFacts facts, DokployOptions options, DateTime checkedAt) => new()
    {
        Checks =
        [
            OperatingSystem(facts),
            Architecture(facts),
            Virtualization(facts),
            Privileges(facts),
            Memory(facts, options),
            Disk(facts, options),
            Docker(facts),
            Swarm(facts),
            Ports(facts, options),
            Internet(facts)
        ],
        IsRoot = facts.UserId == 0,
        BashAvailable = facts.BashAvailable,
        CheckedAt = checkedAt
    };

    private static DokployCompatibilityCheckDto OperatingSystem(DokployHostFacts facts)
    {
        const string key = "os";
        const string title = "İşletim sistemi";
        var name = facts.OsName ?? facts.OsId ?? "Bilinmiyor";

        if (!string.Equals(facts.Kernel, "Linux", StringComparison.OrdinalIgnoreCase))
            return Failed(key, title, $"Dokploy yalnızca Linux'ta çalışır ({facts.Kernel ?? "bilinmeyen çekirdek"}).");

        if (facts.OsId is not null && SupportedDistributions.Contains(facts.OsId))
            return Passed(key, title, name);

        return Warning(key, title, $"{name} resmi olarak desteklenen dağıtımlar arasında değil (Ubuntu, Debian, Fedora, CentOS/RHEL). Kurulum başarısız olabilir.");
    }

    private static DokployCompatibilityCheckDto Architecture(DokployHostFacts facts)
    {
        const string key = "arch";
        const string title = "İşlemci mimarisi";
        var arch = facts.Architecture ?? "bilinmiyor";

        return facts.Architecture is not null && SupportedArchitectures.Contains(facts.Architecture)
            ? Passed(key, title, arch)
            : Warning(key, title, $"{arch} mimarisi için Dokploy image'ı bulunmayabilir.");
    }

    private static DokployCompatibilityCheckDto Virtualization(DokployHostFacts facts)
    {
        const string key = "container";
        const string title = "Çalışma ortamı";

        return facts.ContainerKind switch
        {
            "docker" => Failed(key, title, "Sunucu bir Docker container'ı içinde çalışıyor; kurulum betiği container içinde çalışmayı reddeder."),
            "lxc" => Warning(key, title, "LXC container algılandı; betik servisleri dnsrr modunda kurar, servis keşfi sınırlı olabilir."),
            _ => Passed(key, title, "Sanal veya fiziksel makine")
        };
    }

    private static DokployCompatibilityCheckDto Privileges(DokployHostFacts facts)
    {
        const string key = "privileges";
        const string title = "Root / sudo yetkisi";

        if (facts.UserId == 0)
            return Passed(key, title, "Bağlantı root kullanıcısıyla yapılıyor.");

        if (facts.UserId is null)
            return Failed(key, title, "Kullanıcı kimliği okunamadı.");

        if (!facts.UseSudo)
            return Failed(key, title, "Kullanıcı root değil ve sunucu ayarlarında sudo kullanımı kapalı. Kurulum betiği root yetkisi ister.");

        return facts.SudoWorks == true
            ? Passed(key, title, "Betik sudo ile root olarak çalıştırılacak.")
            : Failed(key, title, facts.SudoError ?? "sudo ile komut çalıştırılamadı.");
    }

    private static DokployCompatibilityCheckDto Memory(DokployHostFacts facts, DokployOptions options)
    {
        const string key = "memory";
        const string title = "Bellek (RAM)";

        if (facts.MemoryKb is not { } memoryKb)
            return Warning(key, title, "Bellek miktarı okunamadı.");

        var memoryMb = memoryKb / 1024;
        var text = FormatGb(memoryMb / 1024d);
        return memoryMb >= options.MinMemoryMb * 0.95
            ? Passed(key, title, $"{text} toplam bellek")
            : Failed(key, title, $"{text} toplam bellek; Dokploy en az {FormatGb(options.MinMemoryMb / 1024d)} ister.");
    }

    private static DokployCompatibilityCheckDto Disk(DokployHostFacts facts, DokployOptions options)
    {
        const string key = "disk";
        const string title = "Boş disk alanı";

        if (facts.DiskAvailableKb is not { } diskKb)
            return Warning(key, title, "Boş disk alanı okunamadı.");

        var diskGb = diskKb / 1024d / 1024d;
        var text = FormatGb(diskGb);
        return diskGb >= options.RecommendedDiskGb
            ? Passed(key, title, $"Kök dizinde {text} boş")
            : Warning(key, title, $"Kök dizinde {text} boş; uygulamalar ve image'lar için en az {options.RecommendedDiskGb} GB önerilir.");
    }

    private static DokployCompatibilityCheckDto Docker(DokployHostFacts facts)
    {
        const string key = "docker";
        const string title = "Docker";

        if (!facts.DockerInstalled)
            return Warning(key, title, "Docker kurulu değil; kurulum betiği Docker'ı otomatik kurar.");

        return facts.DockerVersion is not null
            ? Passed(key, title, $"Docker {facts.DockerVersion} çalışıyor.")
            : Warning(key, title, facts.DockerError ?? "Docker kurulu ancak servisine erişilemedi.");
    }

    private static DokployCompatibilityCheckDto Swarm(DokployHostFacts facts)
    {
        const string key = "swarm";
        const string title = "Docker Swarm";

        if (facts.DokployServiceExists)
            return Failed(key, title, "Dokploy bu sunucuda zaten kurulu.");

        return string.Equals(facts.SwarmState, "active", StringComparison.OrdinalIgnoreCase)
            ? Failed(key, title, "Sunucu zaten bir Docker Swarm'a üye. Kurulum betiği swarm'dan zorla ayrılır (docker swarm leave --force) ve mevcut servisler bozulur.")
            : Passed(key, title, "Sunucu bir swarm'a üye değil; betik yeni swarm başlatır.");
    }

    private static DokployCompatibilityCheckDto Ports(DokployHostFacts facts, DokployOptions options)
    {
        const string key = "ports";
        var required = options.RequiredPorts.Distinct().Order().ToList();
        var title = $"Portlar ({string.Join(", ", required)})";

        if (facts.ListeningPorts is null)
            return Warning(key, title, "Port kontrolü yapılamadı (ss veya netstat bulunamadı).");

        var busy = required.Where(facts.ListeningPorts.Contains).ToList();
        return busy.Count == 0
            ? Passed(key, title, "Gerekli portların hepsi boş.")
            : Failed(key, title, $"Kullanımda olan port: {string.Join(", ", busy)}. Bu portları kullanan servisleri durdurun.");
    }

    private static DokployCompatibilityCheckDto Internet(DokployHostFacts facts)
    {
        const string key = "internet";
        const string title = "İnternet bağlantısı";

        if (!facts.CurlAvailable)
            return Failed(key, title, "curl kurulu değil; kurulum betiği indirilemez.");

        if (!facts.ScriptReachable)
            return Failed(key, title, facts.ScriptError ?? "Kurulum betiğine erişilemiyor.");

        return facts.RegistryReachable
            ? Passed(key, title, "Kurulum betiğine ve Docker Hub'a erişiliyor.")
            : Failed(key, title, facts.RegistryError ?? "Docker Hub'a erişilemiyor; image'lar indirilemez.");
    }

    private static string FormatGb(double value) =>
        value.ToString(value >= 10 ? "0" : "0.#", CultureInfo.GetCultureInfo("tr-TR")) + " GB";

    private static DokployCompatibilityCheckDto Passed(string key, string title, string detail) =>
        new(key, title, DokployCheckStatus.Passed, detail);

    private static DokployCompatibilityCheckDto Warning(string key, string title, string detail) =>
        new(key, title, DokployCheckStatus.Warning, detail);

    private static DokployCompatibilityCheckDto Failed(string key, string title, string detail) =>
        new(key, title, DokployCheckStatus.Failed, detail);
}
