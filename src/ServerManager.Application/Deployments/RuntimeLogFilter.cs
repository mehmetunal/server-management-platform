using System.Text.RegularExpressions;

namespace ServerManager.Application.Deployments;

/// <summary>Çalışma loglarında "yalnızca hata/uyarı" filtresi: error, fatal, panic, exception veya warn geçen satırlar (büyük/küçük harf duyarsız).</summary>
public static partial class RuntimeLogFilter
{
    /// <summary>İstemci tarafındaki filtrenin de kullandığı desen (JavaScript ile uyumlu).</summary>
    public const string Pattern = "error|fatal|panic|exception|warn";

    public static bool IsProblem(string? line) => !string.IsNullOrEmpty(line) && ProblemPattern().IsMatch(line);

    [GeneratedRegex(Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProblemPattern();
}
