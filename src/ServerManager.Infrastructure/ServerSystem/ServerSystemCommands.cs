using System.Globalization;
using ServerManager.Application.ServerSystem;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.ServerSystem;

internal static class ServerSystemCommands
{
    public const string SectionPrefix = "@@ss:";

    public static readonly string Services = Wrap("""
        if command -v systemctl >/dev/null 2>&1 && [ -d /run/systemd/system ]; then
          echo @@ss:systemd
          systemctl list-units --type=service --all --no-pager --no-legend --plain 2>/dev/null
          echo @@ss:unitfiles
          systemctl list-unit-files --type=service --no-pager --no-legend 2>/dev/null
        elif command -v rc-status >/dev/null 2>&1 && [ -d /run/openrc ]; then
          echo @@ss:openrc
          rc-status -a 2>/dev/null
          echo @@ss:enabled
          rc-update show 2>/dev/null
        else
          echo @@ss:none
        fi
        echo @@ss:end
        """);

    public static readonly string Processes = Wrap("""
        if ps --version 2>/dev/null | grep -q procps; then
          echo @@ss:procps
          ps -eo pid=,ppid=,user:32=,pcpu=,pmem=,rss=,etimes=,args= --sort=-pcpu 2>/dev/null
        else
          echo @@ss:busybox
          ps -o pid,ppid,user,rss,args 2>/dev/null
        fi
        echo @@ss:end
        """);

    public static readonly string LogSources = Wrap("""
        echo @@ss:journal
        command -v journalctl >/dev/null 2>&1 && echo yes
        echo @@ss:files
        find /var/log -maxdepth 2 -type f ! -name "*.gz" ! -name "*.xz" ! -name "*.bz2" ! -name "*.zst" ! -name "*.[0-9]" ! -name "*.journal" ! -name "*.journal~" 2>/dev/null | sort | head -n 300
        echo @@ss:end
        """);

    public static readonly string Network = Wrap("""
        echo @@ss:hostname
        hostname 2>/dev/null || cat /etc/hostname 2>/dev/null
        echo @@ss:addr
        ip -o addr show 2>/dev/null
        echo @@ss:link
        ip -o link show 2>/dev/null
        echo @@ss:dev
        cat /proc/net/dev 2>/dev/null
        echo @@ss:route
        ip route show 2>/dev/null || route -n 2>/dev/null
        echo @@ss:dns
        cat /etc/resolv.conf 2>/dev/null
        echo @@ss:ports
        if command -v ss >/dev/null 2>&1; then
          echo tool=ss; ss -H -tulnp 2>/dev/null || ss -tulnp 2>/dev/null
        elif command -v netstat >/dev/null 2>&1; then
          echo tool=netstat; netstat -tulnp 2>/dev/null || netstat -tuln 2>/dev/null
        fi
        echo @@ss:end
        """);

    public static readonly string Storage = Wrap("""
        echo @@ss:df
        df -PkT 2>/dev/null || df -Pk 2>/dev/null
        echo @@ss:inodes
        df -Pi 2>/dev/null
        echo @@ss:lsblk
        command -v lsblk >/dev/null 2>&1 && lsblk -b -P -o NAME,TYPE,SIZE,MOUNTPOINT,FSTYPE,MODEL 2>/dev/null
        echo @@ss:end
        """);

    public static string ControlService(ServiceManagerKind manager, string name, ServiceAction action)
    {
        var verb = action switch
        {
            ServiceAction.Start => "start",
            ServiceAction.Stop => "stop",
            _ => "restart"
        };

        return manager == ServiceManagerKind.OpenRc
            ? $"rc-service {ShellQuote.Quote(name)} {verb} 2>&1"
            : $"systemctl {verb} {ShellQuote.Quote(name)} 2>&1";
    }

    public static string SignalProcess(int pid, ProcessSignal signal) =>
        string.Create(CultureInfo.InvariantCulture, $"kill -{(int)signal} {pid} 2>&1");

    public static string ReadJournal(int lines, string? unit, int? priority)
    {
        var command = string.Create(CultureInfo.InvariantCulture, $"journalctl --no-pager -o short-iso -n {lines}");
        if (!string.IsNullOrEmpty(unit))
            command += " -u " + ShellQuote.Quote(unit);
        if (priority is not null)
            command += string.Create(CultureInfo.InvariantCulture, $" -p {priority.Value}");
        return command + " 2>&1";
    }

    public const int LogOutsideRootExitCode = 3;

    /// <summary>
    /// Sembolik bağlantılar sunucuda çözülür (realpath); gerçek yol hâlâ /var/log altında ve normal bir dosyaysa okunur.
    /// Böylece sudo ile çalışan tail, /var/log altındaki bir bağlantı üzerinden /etc/shadow gibi dosyaları okuyamaz.
    /// </summary>
    public static string ReadFile(int lines, string path) =>
        Wrap(string.Create(CultureInfo.InvariantCulture,
            $"""
            p={ShellQuote.Quote(path)}
            root=$(readlink -f -- {ShellQuote.Quote(ServerSystemRules.LogRoot.TrimEnd('/'))} 2>/dev/null || realpath -- {ShellQuote.Quote(ServerSystemRules.LogRoot.TrimEnd('/'))} 2>/dev/null) || root=
            r=$(readlink -f -- "$p" 2>/dev/null || realpath -- "$p" 2>/dev/null) || r=
            if [ -z "$root" ] || [ -z "$r" ]; then echo 'Log dosyası bulunamadı.'; exit {LogOutsideRootExitCode}; fi
            case "$r" in
              "$root"/*) ;;
              *) echo 'Log dosyası /var/log dışını gösteriyor; okunmadı.'; exit {LogOutsideRootExitCode} ;;
            esac
            if [ ! -f "$r" ]; then echo 'Yalnızca normal log dosyaları okunabilir.'; exit {LogOutsideRootExitCode}; fi
            tail -n {lines} -- "$r" 2>&1
            """));

    private static string Wrap(string script) => "sh -c " + ShellQuote.Quote(script.Replace("\r\n", "\n"));
}
