using System.Globalization;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Dokploy;

/// <summary>Dokploy için sunucuda çalıştırılan komutlar. Dışarıdan gelen her değer <see cref="ShellQuote"/> ile kaçışlanır.</summary>
internal static class DokployCommands
{
    public const string DockerInfo = "docker info --format '{{json .}}'";
    public const string ListServices = "docker service ls --format '{{json .}}'";
    public const string ListContainers = "docker ps -a --filter name=dokploy --format '{{json .}}'";
    public const string SudoCheck = "true";

    /// <summary>Tek SSH komutuyla sistem bilgilerini "anahtar=değer" satırları olarak basar; yetki gerektirmez.</summary>
    public static readonly string HostFacts = "sh -c " + ShellQuote.Quote(
        """
        if [ -r /etc/os-release ]; then . /etc/os-release; fi
        echo "os_id=${ID:-}"
        echo "os_version=${VERSION_ID:-}"
        echo "os_name=${PRETTY_NAME:-}"
        echo "kernel=$(uname -s)"
        echo "arch=$(uname -m)"
        echo "uid=$(id -u)"
        if [ -f /.dockerenv ]; then echo container=docker
        elif grep -qa container=lxc /proc/1/environ 2>/dev/null; then echo container=lxc
        else echo container=none; fi
        echo "mem_kb=$(awk '/^MemTotal:/{print $2}' /proc/meminfo 2>/dev/null)"
        echo "disk_kb=$(df -Pk / 2>/dev/null | awk 'NR==2{print $4}')"
        for tool in curl bash docker; do
          if command -v "$tool" >/dev/null 2>&1; then echo "has_$tool=1"; else echo "has_$tool=0"; fi
        done
        if command -v ss >/dev/null 2>&1; then
          echo ports_tool=ss
          ss -tuln 2>/dev/null | awk 'NR>1{print "listen="$5}'
        elif command -v netstat >/dev/null 2>&1; then
          echo ports_tool=netstat
          netstat -tuln 2>/dev/null | awk '$1 ~ /^(tcp|udp)/{print "listen="$4}'
        else
          echo ports_tool=none
        fi
        """);

    /// <summary>Adrese erişimi denetler; HTTP durum kodunu basar. <paramref name="failOnHttpError"/> kapalıysa 401 gibi yanıtlar da erişim sayılır.</summary>
    public static string Reachability(string url, bool failOnHttpError) =>
        $"curl -sS{(failOnHttpError ? "f" : string.Empty)}L -o /dev/null --max-time 15 -w '%{{http_code}}' {ShellQuote.Quote(url)}";

    public static string LocalHealth(int port)
    {
        var url = ShellQuote.Quote(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/api/health"));
        return "sh -c " + ShellQuote.Quote($"curl -fsS --max-time 5 {url} 2>/dev/null || wget -qO- -T 5 {url}");
    }

    public static string Download(string url, string path) =>
        $"curl -fsSL --max-time 120 -o {ShellQuote.Quote(path)} {ShellQuote.Quote(url)}";

    public static string RestrictPermissions(string path) =>
        $"chmod 600 {ShellQuote.Quote(path)}";

    public static string Sha256(string path) =>
        $"sha256sum {ShellQuote.Quote(path)}";

    /// <param name="version">Doğrulanmış sürüm etiketi; boşsa betik son kararlı sürümü seçer.</param>
    public static string RunScript(string path, string? version, bool useBash)
    {
        var shell = useBash ? "bash" : "sh";
        var prefix = version is null ? string.Empty : $"env DOKPLOY_VERSION={ShellQuote.Quote(version)} ";
        return $"{prefix}{shell} {ShellQuote.Quote(path)}";
    }

    public static string Remove(string path) =>
        $"rm -f {ShellQuote.Quote(path)}";
}
