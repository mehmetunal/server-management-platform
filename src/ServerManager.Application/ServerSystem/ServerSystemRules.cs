using System.Text.RegularExpressions;

namespace ServerManager.Application.ServerSystem;

public static partial class ServerSystemRules
{
    public const string LogRoot = "/var/log/";

    /// <summary>systemd/OpenRC servis adı; '-' ile başlayamaz (komut seçeneği sanılmasın).</summary>
    public static bool IsValidServiceName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= 200 && ServiceNamePattern().IsMatch(name);

    /// <summary>Yalnızca /var/log altındaki dosyalar okunabilir; '..' ve özel karakterler reddedilir.</summary>
    public static bool IsValidLogPath(string? path) =>
        !string.IsNullOrEmpty(path)
        && path.Length <= 300
        && path.StartsWith(LogRoot, StringComparison.Ordinal)
        && !path.Contains("..", StringComparison.Ordinal)
        && LogPathPattern().IsMatch(path);

    public static int NormalizeLines(int lines) => Math.Clamp(lines <= 0 ? LogRequest.DefaultLines : lines, 10, LogRequest.MaxLines);

    public static bool IsValidPriority(int? priority) => priority is null or >= 0 and <= 7;

    public static IReadOnlyList<string> ApplyFilter(IReadOnlyList<string> lines, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return lines;

        var term = filter.Trim();
        return lines.Where(l => l.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    [GeneratedRegex(@"^[A-Za-z0-9_@.:\\][A-Za-z0-9_@.:\\-]*$")]
    private static partial Regex ServiceNamePattern();

    [GeneratedRegex(@"^/var/log/[A-Za-z0-9_@.+/-]+$")]
    private static partial Regex LogPathPattern();
}
