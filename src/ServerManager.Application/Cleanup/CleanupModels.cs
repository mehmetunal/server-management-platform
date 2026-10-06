namespace ServerManager.Application.Cleanup;

/// <summary>Temizlik sayfasındaki gruplar; sıra, ekrandaki ve çalıştırmadaki sıradır (önce container'lar, sonra imajlar …).</summary>
public enum CleanupCategory
{
    Containers = 1,
    Images = 2,
    Networks = 3,
    Volumes = 4,
    BuildCache = 5,
    PackageCache = 6,
    Journal = 7,
    RotatedLogs = 8,
    TempFiles = 9,
    Snaps = 10,
    Kernels = 11
}

public enum CleanupSafety
{
    /// <summary>Güvenle silinebilir.</summary>
    Safe = 1,

    /// <summary>Dikkat: silmeden önce gerekçeyi okuyun; hiçbir zaman önceden seçilmez.</summary>
    Caution = 2
}

/// <summary>
/// Silinebilecek tek bir öğe. <see cref="Key"/> tarama sonucundaki kimliktir; çalıştırmada sunucu yeniden taranır ve
/// yalnızca hâlâ listede olan anahtarlar işlenir (istekten gelen ad veya komut hiçbir zaman doğrudan kullanılmaz).
/// </summary>
/// <param name="Target">Komuta giren değer: container/imaj kimliği, volume/ağ adı, snap için "ad|revizyon".</param>
/// <param name="SizeBytes">Tahmini kazanılacak alan; bilinmiyorsa null.</param>
/// <param name="CanDelete">false ise yalnızca bilgi ve öneri gösterilir (ör. eski çekirdekler).</param>
public sealed record CleanupItem(
    string Key,
    CleanupCategory Category,
    string Target,
    string Name,
    string? Detail,
    long? SizeBytes,
    CleanupSafety Safety,
    string? Reason,
    bool Preselected,
    bool CanDelete = true);

public sealed record CleanupGroup(
    CleanupCategory Category,
    string Title,
    string Description,
    IReadOnlyList<CleanupItem> Items,
    string? Notice = null)
{
    public long TotalBytes => Items.Where(i => i.CanDelete).Sum(i => i.SizeBytes ?? 0);
}

/// <summary>Kullanıcının seçtiği eşikler; tarama ve çalıştırma aynı değerlerle yapılır.</summary>
public sealed class CleanupOptions
{
    public const int DefaultLogDays = 14;
    public const int DefaultTempDays = 10;
    public const int DefaultJournalMaxMegabytes = 200;

    /// <summary>/var/log altındaki döndürülmüş (gz, .1, .old …) dosyalar bu kadar günden eskiyse silinir.</summary>
    public int LogDays { get; set; } = DefaultLogDays;

    /// <summary>/tmp altındaki dosyalar bu kadar gündür değiştirilmemiş ve okunmamışsa silinir.</summary>
    public int TempDays { get; set; } = DefaultTempDays;

    /// <summary>journalctl --vacuum-size hedefi (MB).</summary>
    public int JournalMaxMegabytes { get; set; } = DefaultJournalMaxMegabytes;
}

/// <summary>Taramanın sonucu: gruplar ve sunucunun genel durumu.</summary>
public sealed record CleanupScan(
    IReadOnlyList<CleanupGroup> Groups,
    CleanupOptions Options,
    bool DockerAvailable,
    string? DockerMessage,
    string? PackageManager,
    long? JournalUsageBytes,
    bool UsesSudo)
{
    public IEnumerable<CleanupItem> Items => Groups.SelectMany(g => g.Items);

    public long ReclaimableBytes => Groups.Sum(g => g.TotalBytes);

    public long SafeReclaimableBytes => Items.Where(i => i.CanDelete && i.Safety == CleanupSafety.Safe).Sum(i => i.SizeBytes ?? 0);
}

public enum CleanupLogLevel
{
    Info = 1,
    Success = 2,
    Warning = 3,
    Error = 4
}

/// <summary>Çalıştırma sırasında canlı gösterilen satır.</summary>
public sealed record CleanupLogEntry(CleanupLogLevel Level, string Message, string? Output = null, string? Key = null);

/// <summary>Çalıştırmanın özeti; <see cref="FreedBytes"/> df ile önce/sonra boş alan farkıdır (önizlemede null).</summary>
public sealed record CleanupRunResult(
    bool DryRun,
    int Succeeded,
    int Failed,
    int Skipped,
    long? FreedBytes,
    long EstimatedBytes,
    IReadOnlyList<string> ProcessedItems);
