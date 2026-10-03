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

    public static string ChangeOwner(string path, string? owner, string? group, bool recursive)
    {
        var spec = (owner ?? string.Empty) + (group is null ? string.Empty : ":" + group);
        return $"chown {(recursive ? "-R " : string.Empty)}-- {ShellQuote.Quote(spec)} {ShellQuote.Quote(path)}";
    }
}
