using System.Globalization;
using System.Text.RegularExpressions;
using ServerManager.Application.Deployments;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.Cleanup;

/// <summary>
/// Ham sunucu bilgisinden temizlik listesini üretir: neyin listeleneceği, güvenlik seviyesi, gerekçe ve ön seçim burada belirlenir.
/// Kurallar: panel ağları (sm-proxy, sm-services) ve Docker'ın kendi ağları hiç listelenmez; volume'lar hiçbir zaman önceden seçilmez;
/// panel projesine / servisine ait kaynaklar "Dikkat" olarak işaretlenir ve yalnızca kullanıcı açıkça seçerse silinir.
/// </summary>
public static partial class CleanupClassifier
{
    public const string BuildCacheKey = "buildcache";
    public const string PackageCacheKey = "packages";
    public const string JournalKey = "journal";
    public const string RotatedLogsKey = "logs";
    public const string TempFilesKey = "tmp";

    /// <summary>Hiçbir koşulda listelenmeyen (silinmeyen) ağlar.</summary>
    public static readonly IReadOnlySet<string> ProtectedNetworks = new HashSet<string>(StringComparer.Ordinal)
    {
        "bridge", "host", "none", "docker_gwbridge", "ingress", DomainNames.ProxyNetwork, DomainNames.ServicesNetwork
    };

    private static readonly HashSet<string> StoppedStates = new(StringComparer.OrdinalIgnoreCase) { "exited", "created", "dead" };

    public static IReadOnlyList<CleanupGroup> Classify(CleanupFacts facts, PanelResourceContext context, CleanupOptions options)
    {
        var groups = new List<CleanupGroup>();
        if (facts.DockerAvailable)
        {
            groups.Add(new CleanupGroup(CleanupCategory.Containers, "Durmuş container'lar",
                "Çalışmayan (exited / created / dead) container'lar. Silinince yazılabilir katmanları da gider; imajlar ve isimli volume'lar kalır.",
                Containers(facts, context)));
            groups.Add(new CleanupGroup(CleanupCategory.Images, "Kullanılmayan imajlar",
                "Hiçbir container'ın (durmuş olanlar dahil) kullanmadığı imajlar. Etiketsiz (dangling) imajlar eski build'lerden kalır.",
                Images(facts, context)));
            groups.Add(new CleanupGroup(CleanupCategory.Networks, "Kullanılmayan ağlar",
                "Hiçbir container'ın bağlı olmadığı ağlar. Panel ağları (sm-proxy, sm-services) ve Docker'ın varsayılan ağları listelenmez.",
                Networks(facts, context)));
            groups.Add(new CleanupGroup(CleanupCategory.Volumes, "Kullanılmayan volume'lar",
                "Hiçbir container'a bağlı olmayan volume'lar. İçindeki veri kalıcı olarak silinir; hiçbiri önceden seçilmez.",
                Volumes(facts, context)));
            groups.Add(new CleanupGroup(CleanupCategory.BuildCache, "Build cache",
                "docker build ara katmanları. Silinince bir sonraki build daha uzun sürebilir.",
                BuildCache(facts)));
        }

        groups.Add(new CleanupGroup(CleanupCategory.PackageCache, "Paket önbelleği",
            "İndirilmiş paket dosyaları; kurulu paketler etkilenmez.",
            PackageCache(facts),
            facts.PackageManager is null ? "apt, dnf veya yum bulunamadı." : null));
        groups.Add(new CleanupGroup(CleanupCategory.Journal, "Journal logları",
            $"systemd journal'ı {options.JournalMaxMegabytes.ToString(CultureInfo.InvariantCulture)} MB'a küçültülür (en eski kayıtlar silinir).",
            Journal(facts, options),
            facts.JournalAvailable ? null : "Bu sunucuda journald yok."));
        groups.Add(new CleanupGroup(CleanupCategory.RotatedLogs, "Eski log dosyaları",
            $"/var/log altındaki döndürülmüş / sıkıştırılmış loglar (*.gz, *.1, *.old …), {options.LogDays.ToString(CultureInfo.InvariantCulture)} günden eski.",
            FileSet(CleanupCategory.RotatedLogs, RotatedLogsKey, "Döndürülmüş log dosyaları", facts.RotatedLogs, options.LogDays, CleanupSafety.Safe, null)));
        groups.Add(new CleanupGroup(CleanupCategory.TempFiles, "Geçici dosyalar (/tmp)",
            $"/tmp altında {options.TempDays.ToString(CultureInfo.InvariantCulture)} gündür değiştirilmemiş ve okunmamış dosyalar. Soketler ve systemd-private klasörleri hariçtir.",
            FileSet(CleanupCategory.TempFiles, TempFilesKey, "Eski geçici dosyalar", facts.TempFiles, options.TempDays, CleanupSafety.Caution,
                "Uzun süre çalışan bir uygulama eski bir geçici dosyayı hâlâ kullanıyor olabilir.")));
        if (facts.SnapAvailable)
        {
            groups.Add(new CleanupGroup(CleanupCategory.Snaps, "Eski snap sürümleri",
                "snap'in geri dönüş için sakladığı devre dışı revizyonlar.",
                Snaps(facts)));
        }

        groups.Add(new CleanupGroup(CleanupCategory.Kernels, "Eski çekirdekler",
            "Panel çekirdek silmez; yalnızca durumu gösterir. Çalışan çekirdek listelenmez.",
            Kernels(facts),
            KernelGuidance(facts.PackageManager)));

        return groups;
    }

    private static List<CleanupItem> Containers(CleanupFacts facts, PanelResourceContext context)
    {
        var items = new List<CleanupItem>();
        foreach (var container in facts.Containers.Where(c => StoppedStates.Contains(c.State)))
        {
            var owner = PanelOwnership.Resolve(container.Name, container.Labels, context);
            var detail = $"{container.Image} · {container.Status}";
            items.Add(owner is null
                ? new CleanupItem("container:" + container.Id, CleanupCategory.Containers, container.Id, container.Name, detail,
                    container.SizeBytes, CleanupSafety.Safe, null, Preselected: true)
                : new CleanupItem("container:" + container.Id, CleanupCategory.Containers, container.Id, container.Name, detail,
                    container.SizeBytes, CleanupSafety.Caution, $"Panel projesine ait — {owner.Label}. Silinirse panel bir sonraki deploy / yeniden başlatmada yeniden oluşturur.",
                    Preselected: false));
        }

        return items;
    }

    private static List<CleanupItem> Images(CleanupFacts facts, PanelResourceContext context)
    {
        var items = new List<CleanupItem>();
        var counted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var image in facts.Images
                     .Where(i => i.Containers == 0)
                     .OrderBy(i => i.IsDangling ? 0 : 1)
                     .ThenByDescending(i => i.SizeBytes))
        {
            // Aynı imajın birden çok etiketi ayrı satırdır; boyut yalnızca bir kez sayılır.
            var firstTag = counted.Add(image.Id);
            var size = firstTag ? image.SizeBytes : (long?)null;
            var created = image.CreatedAt is { } at ? at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            if (image.IsDangling)
            {
                items.Add(new CleanupItem("image:" + image.Id, CleanupCategory.Images, image.Id, ShortId(image.Id),
                    Join("Etiketsiz (dangling)", created), size, CleanupSafety.Safe, null, Preselected: true));
                continue;
            }

            var detail = Join(firstTag ? null : "Aynı imajın başka bir etiketi", created);
            var owner = PanelOwnership.ResolveImage(image.Repository, context);
            items.Add(owner is null
                ? new CleanupItem("image:" + image.Reference, CleanupCategory.Images, image.Reference, image.Reference, detail,
                    size, CleanupSafety.Safe, null, Preselected: true)
                : new CleanupItem("image:" + image.Reference, CleanupCategory.Images, image.Reference, image.Reference, detail,
                    size, CleanupSafety.Caution,
                    $"{owner.Label} — geri dönüş (rollback) imajı. Panel son imajları Deployment:KeepImageCount kadar saklar; silinirse bu sürüme geri dönülemez.",
                    Preselected: false));
        }

        return items;
    }

    private static List<CleanupItem> Networks(CleanupFacts facts, PanelResourceContext context)
    {
        var items = new List<CleanupItem>();
        foreach (var network in facts.UnusedNetworks.Where(n => !ProtectedNetworks.Contains(n.Name)))
        {
            var owner = PanelOwnership.Resolve(network.Name, network.Labels, context);
            items.Add(owner is null
                ? new CleanupItem("network:" + network.Id, CleanupCategory.Networks, network.Id, network.Name, network.Driver,
                    null, CleanupSafety.Safe, null, Preselected: true)
                : new CleanupItem("network:" + network.Id, CleanupCategory.Networks, network.Id, network.Name, network.Driver,
                    null, CleanupSafety.Caution, $"Panel projesine ait — {owner.Label}. Proje yeniden başlatılınca ağ yeniden oluşturulur.",
                    Preselected: false));
        }

        return items;
    }

    private static List<CleanupItem> Volumes(CleanupFacts facts, PanelResourceContext context)
    {
        var items = new List<CleanupItem>();
        foreach (var volume in facts.Volumes.Where(v => v.Links == 0).OrderByDescending(v => v.SizeBytes ?? 0))
        {
            var owner = PanelOwnership.Resolve(volume.Name, volume.Labels, context);
            volume.Labels.TryGetValue(PanelOwnership.ComposeProjectLabel, out var compose);
            var (safety, reason) = owner switch
            {
                { Kind: PanelOwnerKind.Service } => (CleanupSafety.Caution,
                    $"{owner.Label} — servisin verisi (veritabanı dosyaları vb.) bu volume'dadır. Silinirse geri getirilemez."),
                not null => (CleanupSafety.Caution, $"Panel projesine ait — {owner.Label}. İçindeki veri kalıcı olarak silinir."),
                _ when !string.IsNullOrEmpty(compose) => (CleanupSafety.Caution,
                    $"Compose projesi volume'u ({compose}). Proje yeniden başlatıldığında boş olarak oluşturulur; veri geri gelmez."),
                _ when IsAnonymous(volume.Name) => (CleanupSafety.Safe, (string?)null),
                _ => (CleanupSafety.Caution, "İsimli volume; içindeki veri kalıcı olarak silinir.")
            };

            // Volume'lar (anonim olanlar dahil) hiçbir zaman önceden seçilmez.
            items.Add(new CleanupItem("volume:" + volume.Name, CleanupCategory.Volumes, volume.Name,
                IsAnonymous(volume.Name) ? ShortId(volume.Name) : volume.Name,
                IsAnonymous(volume.Name) ? "Anonim volume" : null,
                volume.SizeBytes, safety, reason, Preselected: false));
        }

        return items;
    }

    private static List<CleanupItem> BuildCache(CleanupFacts facts) =>
        facts.BuildCacheReclaimableBytes <= 0
            ? []
            :
            [
                new CleanupItem(BuildCacheKey, CleanupCategory.BuildCache, BuildCacheKey, "Kullanılmayan build cache",
                    $"{facts.BuildCacheEntries.ToString(CultureInfo.InvariantCulture)} kayıt · docker builder prune",
                    facts.BuildCacheReclaimableBytes, CleanupSafety.Safe, null, Preselected: true)
            ];

    private static List<CleanupItem> PackageCache(CleanupFacts facts)
    {
        if (facts.PackageManager is null || facts.PackageCacheBytes is not > 0)
            return [];

        var command = facts.PackageManager == "apt" ? "apt-get clean" : $"{facts.PackageManager} clean all";
        return
        [
            new CleanupItem(PackageCacheKey, CleanupCategory.PackageCache, facts.PackageManager, $"{facts.PackageManager} önbelleği", command,
                facts.PackageCacheBytes, CleanupSafety.Safe, null, Preselected: true)
        ];
    }

    private static List<CleanupItem> Journal(CleanupFacts facts, CleanupOptions options)
    {
        if (!facts.JournalAvailable || facts.JournalBytes is null)
            return [];

        var target = (long)options.JournalMaxMegabytes * 1024 * 1024;
        var reclaimable = Math.Max(0, facts.JournalBytes.Value - target);
        return
        [
            new CleanupItem(JournalKey, CleanupCategory.Journal, JournalKey, "journalctl --vacuum-size",
                $"Şu an {FormatMegabytes(facts.JournalBytes.Value)} · hedef {options.JournalMaxMegabytes.ToString(CultureInfo.InvariantCulture)} MB",
                reclaimable, CleanupSafety.Safe, null, Preselected: reclaimable > 0)
        ];
    }

    private static List<CleanupItem> FileSet(
        CleanupCategory category, string key, string name, FileSetFact files, int days, CleanupSafety safety, string? reason)
    {
        if (files.Count == 0)
            return [];

        var detail = $"{files.Count.ToString(CultureInfo.InvariantCulture)} dosya · {days.ToString(CultureInfo.InvariantCulture)} günden eski";
        return [new CleanupItem(key, category, key, name, detail, files.TotalBytes, safety, reason, Preselected: safety == CleanupSafety.Safe)];
    }

    private static List<CleanupItem> Snaps(CleanupFacts facts) =>
        facts.DisabledSnaps
            .Select(s => new CleanupItem($"snap:{s.Name}|{s.Revision}", CleanupCategory.Snaps, $"{s.Name}|{s.Revision}", s.Name,
                $"Revizyon {s.Revision} (devre dışı)", s.SizeBytes, CleanupSafety.Safe, null, Preselected: true))
            .ToList();

    private static List<CleanupItem> Kernels(CleanupFacts facts) =>
        facts.Kernels
            .Where(k => facts.RunningKernel is null || !k.Version.Equals(facts.RunningKernel, StringComparison.Ordinal))
            .Select(k => new CleanupItem("kernel:" + k.Package, CleanupCategory.Kernels, k.Package, k.Package, k.Version, k.SizeBytes,
                CleanupSafety.Caution,
                "Çalışan çekirdek değil. Silmeden önce sunucunun yeni çekirdekle sorunsuz açıldığından emin olun; panel çekirdek silmez.",
                Preselected: false, CanDelete: false))
            .ToList();

    private static string? KernelGuidance(string? packageManager) => packageManager switch
    {
        "apt" => "Eski çekirdekleri kaldırmak için sunucuda: sudo apt autoremove --purge",
        "dnf" => "Eski çekirdekleri kaldırmak için sunucuda: sudo dnf remove --oldinstallonly",
        "yum" => "Eski çekirdekleri kaldırmak için sunucuda: sudo package-cleanup --oldkernels --count=2",
        _ => null
    };

    private static bool IsAnonymous(string name) => AnonymousVolume().IsMatch(name);

    private static string ShortId(string id)
    {
        var value = id.StartsWith("sha256:", StringComparison.Ordinal) ? id[7..] : id;
        return value.Length > 12 ? value[..12] : value;
    }

    private static string? Join(string? first, string? second) =>
        (first, second) switch
        {
            (null, null) => null,
            (null, _) => second,
            (_, null) => first,
            _ => $"{first} · {second}"
        };

    private static string FormatMegabytes(long bytes) =>
        (bytes / 1024d / 1024d).ToString("0.#", CultureInfo.InvariantCulture) + " MB";

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex AnonymousVolume();
}
