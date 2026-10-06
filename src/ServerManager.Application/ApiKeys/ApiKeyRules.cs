using System.Net;
using System.Net.Sockets;

namespace ServerManager.Application.ApiKeys;

/// <summary>API anahtarı kuralları: kapsam kesişimi, süre seçenekleri, süre dolumu ve IP izin listesi.</summary>
public static class ApiKeyRules
{
    public const int MaxNameLength = 100;
    public const int MaxAllowListEntries = 20;

    /// <summary>Panelde sunulan süre seçenekleri (gün). <c>null</c> = süresiz (yalnızca AllowNoExpiry açıksa).</summary>
    public static readonly IReadOnlyList<int> StandardLifetimes = [30, 90, 365];

    /// <summary>
    /// İstek anındaki etkin izinler: anahtarın kapsamı ile kullanıcının o anki izinlerinin kesişimi. Kullanıcının rolünden
    /// sonradan kaldırılan izin anahtarın kapsamında olsa bile kullanılamaz.
    /// </summary>
    public static IReadOnlySet<string> EffectivePermissions(IEnumerable<string> keyScopes, IEnumerable<string> userPermissions)
    {
        var user = userPermissions as IReadOnlySet<string> ?? userPermissions.ToHashSet(StringComparer.Ordinal);
        return keyScopes.Where(user.Contains).ToHashSet(StringComparer.Ordinal);
    }

    public static IReadOnlyList<int?> LifetimeOptions(ApiKeyOptions options)
    {
        var max = Math.Max(1, options.MaxLifetimeDays);
        var list = StandardLifetimes.Where(d => d <= max).Select(d => (int?)d).ToList();
        if (!list.Contains(max))
            list.Add(max);
        if (options.AllowNoExpiry)
            list.Add(null);
        return list;
    }

    /// <summary>Seçilen süreyi doğrular; geçersizse <paramref name="error"/> doludur.</summary>
    public static bool TryResolveExpiry(int? lifetimeDays, ApiKeyOptions options, DateTime utcNow, out DateTime? expiresAt, out string? error)
    {
        expiresAt = null;
        error = null;
        if (lifetimeDays is null)
        {
            if (options.AllowNoExpiry)
                return true;
            error = "Süresiz anahtar oluşturmaya izin verilmiyor; bir geçerlilik süresi seçin.";
            return false;
        }

        if (lifetimeDays < 1 || lifetimeDays > Math.Max(1, options.MaxLifetimeDays))
        {
            error = $"Geçerlilik süresi 1 ile {Math.Max(1, options.MaxLifetimeDays)} gün arasında olmalıdır.";
            return false;
        }

        expiresAt = utcNow.AddDays(lifetimeDays.Value);
        return true;
    }

    public static bool IsExpired(DateTime? expiresAt, DateTime utcNow) =>
        expiresAt.HasValue && expiresAt.Value <= utcNow;

    /// <summary>
    /// Virgül, boşluk veya satır sonuyla ayrılmış IP / CIDR listesini çözer. Tek IP /32 (IPv6'da /128) olarak alınır.
    /// Boş liste "her yerden" demektir.
    /// </summary>
    public static bool TryParseAllowList(string? text, out IReadOnlyList<IPNetwork> networks, out string? error)
    {
        var result = new List<IPNetwork>();
        networks = result;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var entries = text.Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length > MaxAllowListEntries)
        {
            error = $"En fazla {MaxAllowListEntries} IP / CIDR girilebilir.";
            return false;
        }

        foreach (var entry in entries)
        {
            if (!TryParseEntry(entry, out var network))
            {
                error = $"Geçersiz IP veya CIDR: {entry}";
                return false;
            }

            if (!result.Contains(network))
                result.Add(network);
        }

        return true;
    }

    /// <summary>Saklanan biçim: virgülle ayrılmış CIDR listesi; boşsa null.</summary>
    public static string? FormatAllowList(IReadOnlyList<IPNetwork> networks) =>
        networks.Count == 0 ? null : string.Join(",", networks.Select(n => n.ToString()));

    /// <summary>İzin listesi boşsa her adres; değilse adres listedeki bir ağa düşmelidir. Adres bilinmiyorsa reddedilir.</summary>
    public static bool IsIpAllowed(IReadOnlyList<IPNetwork> networks, IPAddress? remote)
    {
        if (networks.Count == 0)
            return true;
        if (remote is null)
            return false;

        if (remote.IsIPv4MappedToIPv6)
            remote = remote.MapToIPv4();

        return networks.Any(n => n.BaseAddress.AddressFamily == remote.AddressFamily && n.Contains(remote));
    }

    public static IReadOnlyList<string> ParseScopes(string? stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? []
            : stored.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToList();

    public static string FormatScopes(IEnumerable<string> scopes) =>
        string.Join(' ', scopes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    private static bool TryParseEntry(string entry, out IPNetwork network)
    {
        network = default;
        if (entry.Contains('/'))
        {
            if (!IPNetwork.TryParse(entry, out network))
                return false;
            return true;
        }

        if (!IPAddress.TryParse(entry, out var address))
            return false;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        network = new IPNetwork(address, address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128);
        return true;
    }
}
