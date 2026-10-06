using System.Globalization;
using ServerManager.Application.ResourceUsage;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.ServerSystem;

/// <summary>Kaynak Kullanımı sayfasının salt okunur komutları.</summary>
internal static class ResourceCommands
{
    /// <summary>
    /// Yük, bellek, 1 saniyelik CPU dağılımı (vmstat yoksa /proc/stat iki kez okunur), swap kullanan process'ler (swap doluysa),
    /// son 24 saatin OOM kayıtları ve pidstat varsa G/Ç yapan process'ler.
    /// </summary>
    public static readonly string Snapshot = Wrap("""
        LC_ALL=C; export LC_ALL
        echo @@ss:nproc
        nproc 2>/dev/null || grep -c '^processor' /proc/cpuinfo 2>/dev/null
        echo @@ss:loadavg
        cat /proc/loadavg 2>/dev/null
        echo @@ss:uptime
        cat /proc/uptime 2>/dev/null
        echo @@ss:meminfo
        cat /proc/meminfo 2>/dev/null
        echo @@ss:cpu
        if command -v vmstat >/dev/null 2>&1; then
          echo tool=vmstat; vmstat 1 2 2>/dev/null
        else
          echo tool=proc; head -n 1 /proc/stat; sleep 1; head -n 1 /proc/stat
        fi
        echo @@ss:swap
        if awk '/^SwapTotal:/ { t = $2 } /^SwapFree:/ { f = $2 } END { exit !(t > f) }' /proc/meminfo 2>/dev/null; then
          grep -H -e '^Name:' -e '^VmSwap:' /proc/[0-9]*/status 2>/dev/null
        fi
        echo @@ss:oom
        if command -v journalctl >/dev/null 2>&1; then
          echo source=journal
          journalctl -k --since '24 hours ago' --no-pager -q -o short-iso 2>/dev/null | grep -iE 'out of memory|oom-kill|killed process' | tail -n 20
        else
          echo source=dmesg
          dmesg 2>/dev/null | grep -iE 'out of memory|oom-kill|killed process' | tail -n 20
        fi
        echo @@ss:io
        if command -v pidstat >/dev/null 2>&1; then echo tool=pidstat; pidstat -d 1 1 2>/dev/null; fi
        echo @@ss:end
        """);

    /// <summary>
    /// En büyük klasörler (du -x -d 2) ve dosyalar (find -xdev -size +N). Her biri timeout ile sınırlıdır ve
    /// nice / ionice ile düşük öncelikte çalışır (araç yoksa atlanır). GNU du'da küçük klasörler --threshold ile elenir.
    /// </summary>
    public static string DiskScan(string path)
    {
        var limit = ResourceRules.ScanTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        var threshold = ResourceRules.DirectoryThresholdMegabytes.ToString(CultureInfo.InvariantCulture);
        // BusyBox find "M" sonekini tanımaz; k (KiB) hem GNU hem BusyBox'ta çalışır.
        var large = (ResourceRules.LargeFileMegabytes * 1024).ToString(CultureInfo.InvariantCulture);
        var directories = ResourceRules.MaxDirectories.ToString(CultureInfo.InvariantCulture);
        var files = ResourceRules.MaxFiles.ToString(CultureInfo.InvariantCulture);
        return Wrap($$"""
            LC_ALL=C; export LC_ALL
            p={{ShellQuote.Quote(path)}}
            pre=""
            command -v timeout >/dev/null 2>&1 && pre="timeout {{limit}}"
            command -v nice >/dev/null 2>&1 && pre="$pre nice -n 19"
            command -v ionice >/dev/null 2>&1 && pre="$pre ionice -c3"
            th=""
            du --version >/dev/null 2>&1 && th="--threshold={{threshold}}M"
            echo @@ss:du
            out=$($pre du -x -k -d 2 $th -- "$p" 2>/dev/null); rc=$?
            printf '%s\n' "$out" | sort -rn | head -n {{directories}}
            echo @@ss:dustatus
            echo "$rc"
            echo @@ss:files
            out=$($pre find "$p" -xdev -type f -size +{{large}}k -exec stat -c '%s|%Y|%U|%n' {} + 2>/dev/null); rc=$?
            printf '%s\n' "$out" | sort -t'|' -k1,1nr | head -n {{files}}
            echo @@ss:filesstatus
            echo "$rc"
            echo @@ss:end
            """);
    }

    private static string Wrap(string script) => "sh -c " + ShellQuote.Quote(script.Replace("\r\n", "\n"));
}
