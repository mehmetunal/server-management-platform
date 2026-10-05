using System.Text;
using System.Text.RegularExpressions;

namespace ServerManager.Application.Deployments;

/// <summary>
/// Proje klasörüne yazılan <c>.env</c> içeriği: <c>ANAHTAR=değer</c> satırları, boş satır ve <c>#</c> yorumları.
/// Docker'ın <c>--env-file</c> biçimidir; değerler tırnaksız yazılır.
/// </summary>
public static partial class EnvironmentFile
{
    public const int MaxLength = 65536;
    public const int MaxEntries = 500;

    public static bool TryParse(string? text, out IReadOnlyList<string> keys, out string? error)
    {
        keys = [];
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (text.Length > MaxLength)
        {
            error = $"Ortam değişkenleri en fazla {MaxLength / 1024} KB olabilir.";
            return false;
        }

        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lineNumber = 0;
        foreach (var rawLine in Normalize(text).Split('\n'))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            var key = separator < 0 ? line : line[..separator].Trim();
            if (separator < 0 || !KeyPattern().IsMatch(key))
            {
                error = $"{lineNumber}. satır ANAHTAR=değer biçiminde olmalı; anahtar harf veya alt çizgiyle başlar, harf, rakam ve alt çizgi içerir.";
                return false;
            }

            if (!seen.Add(key))
            {
                error = $"{key} anahtarı birden fazla kez tanımlanmış.";
                return false;
            }

            found.Add(key);
        }

        if (found.Count > MaxEntries)
        {
            error = $"En fazla {MaxEntries} ortam değişkeni tanımlanabilir.";
            return false;
        }

        keys = found;
        return true;
    }

    /// <summary>Satır sonlarını LF yapar, satır sonu boşluklarını atar ve dosyayı tek satır sonuyla bitirir.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder();
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
            builder.Append(line.TrimEnd()).Append('\n');

        return builder.ToString().TrimEnd('\n') + "\n";
    }

    /// <summary>Anahtar harf veya alt çizgiyle başlar; harf, rakam ve alt çizgi içerir.</summary>
    public static bool IsValidKey(string? key) => !string.IsNullOrEmpty(key) && KeyPattern().IsMatch(key);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex KeyPattern();
}
