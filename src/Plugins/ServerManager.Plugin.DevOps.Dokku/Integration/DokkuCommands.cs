using System.Text.RegularExpressions;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Plugin.DevOps.Dokku.Integration;

/// <summary>Dokku için sunucuda çalıştırılan komutlar. Sürüm ve uygulama adı kabuğa kaçışlanarak yazılır.</summary>
internal static partial class DokkuCommands
{
    public static readonly string Probe = "sh -c " + ShellQuote.Quote(
        """
        if ! command -v dokku >/dev/null 2>&1; then
          echo SM_STATE=missing
          exit 0
        fi
        echo SM_STATE=installed
        ver=$(dokku version 2>/dev/null | head -n 1 | tr -d '\r')
        echo "SM_VERSION=${ver}"
        apps=$(dokku apps:list --quiet 2>/dev/null || dokku apps:list 2>/dev/null | awk 'NR>1 && $0 !~ /^====/ {print $1}')
        printf '%s\n' "$apps" | while IFS= read -r app; do
          case "$app" in
            [a-z0-9]*) ;;
            *) continue ;;
          esac
          case "$app" in
            *[!a-z0-9-]*) continue ;;
          esac
          report=$(dokku ps:report "$app" 2>/dev/null)
          domains=$(dokku domains:report "$app" 2>/dev/null)
          deployed=$(printf '%s\n' "$report" | awk -F': ' '/^[[:space:]]*Deployed:/{print $2; exit}')
          running=$(printf '%s\n' "$report" | awk -F': ' '/^[[:space:]]*Running:/{print $2; exit}')
          vhosts=$(printf '%s\n' "$domains" | awk -F': ' '/Domains app vhosts:/{print $2; exit}')
          printf 'SM_APP=%s|%s|%s|%s\n' "$app" "$deployed" "$running" "$vhosts"
        done
        """);

    public static string Restart(string app) => "dokku ps:restart " + ShellQuote.Quote(app);

    /// <summary>
    /// Kurulum betiği mktemp ile oluşturulan, yalnızca root'un yazabildiği benzersiz bir dosyaya indirilir; sabit /tmp yolu
    /// kullanılmaz (başka bir kullanıcı önceden dosya veya sembolik bağlantı koyup root'a kendi betiğini çalıştıramasın).
    /// </summary>
    public static string Install(string version)
    {
        var url = "https://dokku.com/install/" + version + "/bootstrap.sh";
        var script = string.Join('\n',
        [
            "set -eu",
            "dir=$(mktemp -d \"${TMPDIR:-/tmp}/sm-dokku.XXXXXXXXXX\")",
            "trap 'rm -rf -- \"$dir\"' EXIT",
            "chmod 700 \"$dir\"",
            "path=\"$dir/bootstrap.sh\"",
            "curl -fsSL --max-time 120 -o \"$path\" " + ShellQuote.Quote(url),
            "chmod 600 \"$path\"",
            "env DOKKU_TAG=" + ShellQuote.Quote(version) + " bash \"$path\""
        ]);
        return "bash -c " + ShellQuote.Quote(script);
    }

    public static bool IsVersion(string? version) => version is not null && VersionPattern().IsMatch(version);

    public static bool IsAppName(string? app) => app is not null && AppNamePattern().IsMatch(app);

    [GeneratedRegex(@"^v(?:0|[1-9]\d{0,3})\.(?:0|[1-9]\d{0,3})\.(?:0|[1-9]\d{0,3})$")]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]{0,62})$")]
    private static partial Regex AppNamePattern();
}
