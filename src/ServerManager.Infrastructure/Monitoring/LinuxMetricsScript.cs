namespace ServerManager.Infrastructure.Monitoring;

internal static class LinuxMetricsScript
{
    public const string EndMarker = "@@END";

    // Tek tırnak kullanılmamalı: betik sh -c '...' içinde çalışır.
    private const string Script = """
        export LC_ALL=C
        echo @@T1; cat /proc/uptime
        echo @@STAT1; head -n 1 /proc/stat
        echo @@NET1; cat /proc/net/dev
        sleep 1
        echo @@T2; cat /proc/uptime
        echo @@STAT2; head -n 1 /proc/stat
        echo @@NET2; cat /proc/net/dev
        echo @@THREADS; grep -c "^processor" /proc/cpuinfo 2>/dev/null
        echo @@CORES; grep -E "^(physical id|core id)" /proc/cpuinfo 2>/dev/null | paste - - 2>/dev/null | sort -u | wc -l
        echo @@MEMINFO; cat /proc/meminfo
        echo @@LOADAVG; cat /proc/loadavg
        echo @@DF; df -Pk 2>/dev/null
        echo @@DFI; df -Pi 2>/dev/null
        echo @@PS; ps -eo pid,user,pcpu,pmem,comm --sort=-pcpu 2>/dev/null | head -n 16
        echo @@HOSTNAME; hostname 2>/dev/null || cat /proc/sys/kernel/hostname
        echo @@KERNEL; uname -r
        echo @@ARCH; uname -m
        echo @@OSRELEASE; cat /etc/os-release 2>/dev/null
        echo @@TZ; timedatectl show -p Timezone --value 2>/dev/null || cat /etc/timezone 2>/dev/null || readlink /etc/localtime 2>/dev/null
        echo @@TZABBR; date +%Z
        echo @@TEMP; cat /sys/class/thermal/thermal_zone0/temp 2>/dev/null
        echo @@ADDR; ip -o addr show 2>/dev/null
        echo @@END
        """;

    public static string Command => "sh -c '" + Script.Replace("\r\n", "\n") + "'";
}
