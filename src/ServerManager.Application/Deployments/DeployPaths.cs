using System.Text.RegularExpressions;
using ServerManager.Application.Files;

namespace ServerManager.Application.Deployments;

/// <summary>Sunucudaki proje klasörü ve proje içindeki dosya yolları.</summary>
public static partial class DeployPaths
{
    public const int MaxLength = 500;

    /// <summary>Bu klasörlerin altına deploy edilemez; içerikleri işletim sistemine aittir.</summary>
    private static readonly string[] SystemRoots = ["/bin", "/boot", "/dev", "/etc", "/lib", "/lib32", "/lib64", "/proc", "/run", "/sbin", "/sys", "/usr"];

    public static bool TryValidate(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Deploy klasörü zorunludur.";
            return false;
        }

        if (path.Length > MaxLength || !AbsolutePathPattern().IsMatch(path) || RemotePath.Normalize(path) != path)
        {
            error = "Mutlak ve sade bir yol girin (ör. /srv/apps/api); harf, rakam, nokta, alt çizgi, tire ve / kullanılabilir.";
            return false;
        }

        if (path.Count(c => c == '/') < 2 || FileManagerOptions.DefaultProtectedPaths.Contains(path, StringComparer.Ordinal))
        {
            error = "Sistem klasörünün kendisi seçilemez; altında projeye özel bir klasör belirtin (ör. /srv/apps/api).";
            return false;
        }

        if (SystemRoots.Any(root => RemotePath.IsSameOrDescendant(path, root)))
        {
            error = "Sistem klasörlerinin (/etc, /usr, /bin, /proc …) altına deploy edilemez.";
            return false;
        }

        if (!IsProjectDepth(path))
        {
            error = "Kullanıcı ve servis klasörlerinin kendisi seçilemez (ör. /home/ubuntu, /root/.ssh, /var/lib/docker); altında projeye özel bir klasör belirtin (ör. /home/ubuntu/apps/api).";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ev ve servis klasörlerinde proje klasörü bir kat daha derinde olmalıdır: /home/&lt;kullanıcı&gt;/&lt;proje&gt;, /root/&lt;proje&gt;,
    /// /var/lib/&lt;servis&gt;/&lt;proje&gt;. Ev klasörünün hemen altındaki gizli klasörler (.ssh, .config …) seçilemez.
    /// </summary>
    private static bool IsProjectDepth(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments[0] switch
        {
            "home" => segments.Length >= 3 && !segments[2].StartsWith('.'),
            "root" => !segments[1].StartsWith('.'),
            "var" when segments[1] == "lib" => segments.Length >= 4,
            _ => true
        };
    }

    /// <summary>Proje içindeki göreli dosya yolu (compose dosyası, Dockerfile); proje klasörünün dışına çıkamaz.</summary>
    public static bool IsValidRelativeFile(string? path) =>
        !string.IsNullOrEmpty(path)
        && path.Length <= 255
        && RelativePathPattern().IsMatch(path)
        && !path.Split('/').Any(segment => segment is "" or "." or "..");

    public static string Combine(string directory, string relative) => directory.TrimEnd('/') + "/" + relative;

    [GeneratedRegex("^/[A-Za-z0-9._/-]+$")]
    private static partial Regex AbsolutePathPattern();

    [GeneratedRegex("^[A-Za-z0-9._/-]+$")]
    private static partial Regex RelativePathPattern();
}
