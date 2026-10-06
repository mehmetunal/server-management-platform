using System.Globalization;
using ServerManager.Application.Cleanup;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.ServerSystem;

/// <summary>
/// Temizlik sayfasının sistem komutları. Komutlara giren tek değişken değerler gün / MB sayıları (doğrulanmış tam sayı) ile
/// snap adı ve revizyonudur (<see cref="ShellQuote"/> ile kaçışlanır). Dosya silme komutları taramadaki find ölçütlerini aynen kullanır;
/// böylece önizlemede listelenenden fazlası silinmez.
/// </summary>
internal static class CleanupCommands
{
    public const string TotalMarker = "@total|";
    public const int PreviewFileCount = 20;

    /// <summary>Döndürülmüş / sıkıştırılmış loglar; journal klasörü ve aktif *.log dosyaları dışarıda kalır.</summary>
    private const string RotatedLogPatterns =
        @"\( -name '*.gz' -o -name '*.xz' -o -name '*.bz2' -o -name '*.zst' -o -name '*.[0-9]' -o -name '*.old' " +
        @"-o -name '*-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]' \)";

    /// <summary>/tmp'de dokunulmayan yerler: systemd ve snap'in özel klasörleri, X/ICE soket klasörleri. -type f soketleri zaten dışarıda bırakır.</summary>
    private const string TempExclusions =
        "! -path '/tmp/systemd-private-*' ! -path '/tmp/snap-private-tmp/*' ! -path '/tmp/.X11-unix/*' ! -path '/tmp/.ICE-unix/*' " +
        "! -path '/tmp/.XIM-unix/*' ! -path '/tmp/.font-unix/*' ! -path '/tmp/.Test-unix/*' ! -name '*.pid' ! -name '*.lock'";

    public static string Scan(CleanupOptions options) => Wrap($$"""
        LC_ALL=C; export LC_ALL
        echo @@ss:pm
        if command -v apt-get >/dev/null 2>&1; then echo "apt $(du -sk /var/cache/apt 2>/dev/null | cut -f1)"
        elif command -v dnf >/dev/null 2>&1; then echo "dnf $(du -sk /var/cache/dnf 2>/dev/null | cut -f1)"
        elif command -v yum >/dev/null 2>&1; then echo "yum $(du -sk /var/cache/yum 2>/dev/null | cut -f1)"
        fi
        echo @@ss:journal
        if command -v journalctl >/dev/null 2>&1; then echo available; journalctl --disk-usage 2>&1; fi
        echo @@ss:logs
        {{ListFiles(RotatedLogsFind(options.LogDays))}}
        echo @@ss:tmp
        {{ListFiles(TempFilesFind(options.TempDays))}}
        echo @@ss:snap
        if command -v snap >/dev/null 2>&1; then echo available; snap list --all 2>/dev/null; fi
        echo @@ss:snapfiles
        stat -c '%s|%n' /var/lib/snapd/snaps/*.snap 2>/dev/null
        echo @@ss:kernel
        uname -r 2>/dev/null
        echo @@ss:kernels
        if command -v dpkg-query >/dev/null 2>&1; then dpkg-query -W -f='${Status}|${Package}|${Installed-Size}\n' 'linux-image-[0-9]*' 2>/dev/null
        elif command -v rpm >/dev/null 2>&1; then rpm -q --qf '%{NAME}|%{VERSION}-%{RELEASE}.%{ARCH}|%{SIZE}\n' kernel kernel-core 2>/dev/null
        fi
        echo @@ss:end
        """);

    /// <summary>/var/log altında, journal klasörü hariç, belirtilen günden eski döndürülmüş log dosyaları (find ifadesi, eylemsiz).</summary>
    public static string RotatedLogsFind(int days) =>
        string.Create(CultureInfo.InvariantCulture,
            $"find /var/log -xdev -type f {RotatedLogPatterns} -mtime +{Days(days)} ! -path '/var/log/journal/*'");

    /// <summary>/tmp altında hem değişiklik hem erişim zamanı belirtilen günden eski normal dosyalar (find ifadesi, eylemsiz).</summary>
    public static string TempFilesFind(int days) =>
        string.Create(CultureInfo.InvariantCulture,
            $"find /tmp -xdev -mindepth 1 -type f -mtime +{Days(days)} -atime +{Days(days)} {TempExclusions}");

    /// <summary>Önizleme: dosya sayısı, toplam boyut ve en büyük dosyalar.</summary>
    public static string PreviewFiles(string find) => Wrap("LC_ALL=C; export LC_ALL\n" + ListFiles(find));

    /// <summary>Taramadaki ölçütlerle siler ve silinen dosya sayısını yazar.</summary>
    public static string DeleteFiles(string find) =>
        Wrap($"{find} -delete -print 2>/dev/null | awk 'END {{ printf \"%d dosya silindi.\\n\", NR }}'");

    public static string? CleanPackages(string manager, bool dryRun) => manager switch
    {
        "apt" => dryRun ? "apt-get -s clean 2>&1" : "apt-get clean 2>&1",
        "dnf" => dryRun ? null : "dnf clean all 2>&1",
        "yum" => dryRun ? null : "yum clean all 2>&1",
        _ => null
    };

    public static string DescribePackages(string manager) => manager == "apt" ? "apt-get clean" : $"{manager} clean all";

    public const string JournalUsage = "journalctl --disk-usage 2>&1";

    public static string VacuumJournal(int megabytes) =>
        string.Create(CultureInfo.InvariantCulture, $"journalctl --vacuum-size={Math.Max(CleanupRules.MinJournalMegabytes, megabytes)}M 2>&1");

    public static string RemoveSnapRevision(string name, string revision) =>
        $"snap remove {ShellQuote.Quote(name)} --revision={ShellQuote.Quote(revision)} 2>&1";

    private static string ListFiles(string find) =>
        find + " -exec stat -c '%s|%Y|%n' {} + 2>/dev/null | sort -t'|' -k1,1nr | " +
        "awk -F'|' '{ c++; s += $1; if (c <= " + PreviewFileCount.ToString(CultureInfo.InvariantCulture) + ") print } " +
        "END { printf \"" + TotalMarker + "%.0f|%.0f\\n\", c, s }'";

    private static string Days(int days) => Math.Clamp(days, CleanupRules.MinDays, CleanupRules.MaxDays).ToString(CultureInfo.InvariantCulture);

    private static string Wrap(string script) => "sh -c " + ShellQuote.Quote(script.Replace("\r\n", "\n"));
}
