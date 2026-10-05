using ServerManager.Application.Files;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Files;

/// <summary>SFTP'nin karşılamadığı dosya işlemleri için kabuk komutları. Tüm yollar tırnaklanır ve "--" ile seçeneklerden ayrılır.</summary>
internal static class RemoteFileCommands
{
    public static string Copy(string source, string destination) =>
        $"cp -a -- {ShellQuote.Quote(source)} {ShellQuote.Quote(destination)}";

    public static string RemoveRecursive(string path) =>
        $"rm -rf -- {ShellQuote.Quote(path)}";

    public static string ChangeMode(string path, string mode, bool recursive) =>
        $"chmod {(recursive ? "-R " : string.Empty)}-- {ShellQuote.Quote(mode)} {ShellQuote.Quote(path)}";

    /// <summary>Alt öğelerde sembolik bağlantılar izlenmez (-P); bağlantının gösterdiği dosyanın sahibi değişmez.</summary>
    public static string ChangeOwner(string path, string? owner, string? group, bool recursive)
    {
        var spec = (owner ?? string.Empty) + (group is null ? string.Empty : ":" + group);
        return $"chown {(recursive ? "-R -P " : string.Empty)}-- {ShellQuote.Quote(spec)} {ShellQuote.Quote(path)}";
    }

    /// <summary>
    /// Yolun kendisinin bağlantı olup olmadığını, bağlantılar izlenerek ulaşılan gerçek yolu ve üst klasörün gerçek yolunu yazar.
    /// Çıktı: <c>SM_LINK=0|1</c>, <c>SM_TARGET=…</c>, <c>SM_PARENT=…</c>.
    /// </summary>
    public static string Resolve(string path) =>
        "sh -c " + ShellQuote.Quote(
            $"p={ShellQuote.Quote(path)}\n" +
            "if [ -L \"$p\" ]; then echo SM_LINK=1; else echo SM_LINK=0; fi\n" +
            "t=$(readlink -f -- \"$p\" 2>/dev/null || realpath -- \"$p\") || exit 1\n" +
            "d=$(dirname -- \"$p\")\n" +
            "d=$(readlink -f -- \"$d\" 2>/dev/null || realpath -- \"$d\") || exit 1\n" +
            "printf 'SM_TARGET=%s\\nSM_PARENT=%s\\n' \"$t\" \"$d\"\n");

    /// <summary><see cref="Resolve"/> çıktısını okur; eksik, göreli veya sade olmayan bir yol varsa null döner.</summary>
    public static RemotePathResolution? ParseResolution(string stdout, string path)
    {
        string? link = null, target = null, parent = null;
        foreach (var line in stdout.Split('\n'))
        {
            if (line.StartsWith("SM_LINK=", StringComparison.Ordinal))
                link = line["SM_LINK=".Length..];
            else if (line.StartsWith("SM_TARGET=", StringComparison.Ordinal))
                target = line["SM_TARGET=".Length..];
            else if (line.StartsWith("SM_PARENT=", StringComparison.Ordinal))
                parent = line["SM_PARENT=".Length..];
        }

        if (link is not ("0" or "1") || !IsCanonical(target) || !IsCanonical(parent))
            return null;

        var entry = path == "/" ? "/" : RemotePath.Combine(parent!, RemotePath.GetFileName(path));
        return new RemotePathResolution(link == "1", target!, entry);
    }

    private static bool IsCanonical(string? path) => path is not null && RemotePath.Normalize(path) == path;
}
