using System.Text;

namespace ServerManager.Application.Deployments;

/// <summary>
/// Kayıtlı <c>.env</c> içeriğini (bkz. <see cref="EnvironmentFile"/>) satır satır düzenler: yorumlar, boş satırlar ve
/// değişkenlerin sırası korunur; değişen anahtar yerinde güncellenir, yeni anahtar sona eklenir, silinen anahtarın
/// satırı kaldırılır. Değerler Docker <c>--env-file</c> biçimindeki gibi tırnaksız ve tek satırdır.
/// </summary>
public sealed class EnvironmentDocument
{
    public const int MaxValueLength = 32768;

    private readonly List<Line> _lines;

    private EnvironmentDocument(List<Line> lines)
    {
        _lines = lines;
    }

    public static EnvironmentDocument Empty() => new([]);

    /// <summary>İçeriği <see cref="EnvironmentFile.TryParse"/> kurallarıyla doğrular ve ayrıştırır.</summary>
    public static bool TryParse(string? text, out EnvironmentDocument document, out string? error)
    {
        document = Empty();
        if (!EnvironmentFile.TryParse(text, out _, out error))
            return false;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        var lines = new List<Line>();
        var normalized = EnvironmentFile.Normalize(text);
        foreach (var raw in normalized[..^1].Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                lines.Add(new Line(null, null, raw));
                continue;
            }

            var separator = trimmed.IndexOf('=', StringComparison.Ordinal);
            lines.Add(new Line(trimmed[..separator].Trim(), trimmed[(separator + 1)..], raw));
        }

        document = new EnvironmentDocument(lines);
        return true;
    }

    public IReadOnlyList<string> Keys => _lines.Where(l => l.Key is not null).Select(l => l.Key!).ToList();

    public int Count => _lines.Count(l => l.Key is not null);

    public bool ContainsKey(string key) => IndexOf(key) >= 0;

    public string? GetValue(string key)
    {
        var index = IndexOf(key);
        return index < 0 ? null : _lines[index].Value;
    }

    /// <summary>Anahtarın değerini değiştirir veya anahtar yoksa sona ekler.</summary>
    /// <returns>Anahtar yeni eklendiyse true.</returns>
    public bool Set(string key, string value)
    {
        var line = new Line(key, value, key + "=" + value);
        var index = IndexOf(key);
        if (index >= 0)
        {
            _lines[index] = line;
            return false;
        }

        // Dosya sonundaki boş satırların önüne eklenir; içerik sonda tek satır sonuyla biter.
        var insertAt = _lines.Count;
        while (insertAt > 0 && _lines[insertAt - 1].Key is null && _lines[insertAt - 1].Raw.Trim().Length == 0)
            insertAt--;

        _lines.Insert(insertAt, line);
        return true;
    }

    public bool Remove(string key)
    {
        var index = IndexOf(key);
        if (index < 0)
            return false;

        _lines.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// <paramref name="incoming"/> içindeki değişkenleri ekler. <paramref name="overwrite"/> false ise var olan anahtarlar
    /// değiştirilmez ve <see cref="EnvironmentMergeResult.Skipped"/> içinde döner.
    /// </summary>
    public EnvironmentMergeResult Merge(IEnumerable<KeyValuePair<string, string>> incoming, bool overwrite)
    {
        var added = new List<string>();
        var updated = new List<string>();
        var skipped = new List<string>();
        foreach (var (key, value) in incoming)
        {
            if (ContainsKey(key))
            {
                if (!overwrite)
                {
                    skipped.Add(key);
                    continue;
                }

                if (!string.Equals(GetValue(key), value, StringComparison.Ordinal))
                    updated.Add(key);

                Set(key, value);
                continue;
            }

            Set(key, value);
            added.Add(key);
        }

        return new EnvironmentMergeResult(added, updated, skipped);
    }

    public IReadOnlyList<KeyValuePair<string, string>> Entries() =>
        _lines.Where(l => l.Key is not null).Select(l => new KeyValuePair<string, string>(l.Key!, l.Value!)).ToList();

    /// <summary><see cref="EnvironmentFile.Normalize"/> biçiminde metin (LF, tek satır sonu ile biter).</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        foreach (var line in _lines)
            builder.Append(line.Raw).Append('\n');

        return EnvironmentFile.Normalize(builder.ToString());
    }

    /// <summary>Anahtar adı kuralı; <see cref="EnvironmentFile"/> ile aynıdır.</summary>
    public static bool IsValidKey(string? key) => EnvironmentFile.IsValidKey(key);

    /// <summary>Değer tek satır olmalı ve sınırı aşmamalı; satır sonu boşlukları <c>.env</c> yazılırken silindiği için kabul edilmez.</summary>
    public static bool TryValidateValue(string? value, out string? error)
    {
        error = null;
        if (value is null)
        {
            error = "Değer zorunludur (boş bırakılabilir).";
            return false;
        }

        if (value.Length > MaxValueLength)
        {
            error = $"Değer en fazla {MaxValueLength / 1024} KB olabilir.";
            return false;
        }

        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            error = "Değer tek satır olmalıdır; çok satırlı değerleri base64 gibi tek satırlık bir biçime çevirin.";
            return false;
        }

        if (value.Length > 0 && char.IsWhiteSpace(value[^1]))
        {
            error = "Değer boşlukla bitemez (.env dosyasında satır sonu boşlukları silinir).";
            return false;
        }

        return true;
    }

    private int IndexOf(string key) => _lines.FindIndex(l => string.Equals(l.Key, key, StringComparison.Ordinal));

    private sealed record Line(string? Key, string? Value, string Raw);
}

public sealed record EnvironmentMergeResult(IReadOnlyList<string> Added, IReadOnlyList<string> Updated, IReadOnlyList<string> Skipped);
