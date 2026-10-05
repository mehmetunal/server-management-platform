using System.Security.Cryptography;
using System.Text;

namespace ServerManager.Application.ManagedServices;

/// <summary>Servis parolası ve anahtar üretimi.</summary>
public static class ServiceSecrets
{
    public const int DefaultPasswordLength = 24;

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";

    /// <summary>
    /// Harf ve rakamlardan güçlü parola; en az bir büyük harf, küçük harf ve rakam içerir (SQL Server kuralını da karşılar).
    /// Sembol kullanılmaz; böylece parola URL'lerde ve bağlantı dizelerinde kaçış gerektirmez.
    /// </summary>
    public static string GeneratePassword(int length = DefaultPasswordLength)
    {
        length = Math.Clamp(length, 12, ServiceValidation.MaxPasswordLength);
        const string all = Upper + Lower + Digits;
        var chars = new char[length];
        chars[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[1] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        for (var i = 3; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    /// <summary>32 bayt rastgele değerin onaltılık gösterimi (n8n şifreleme anahtarı).</summary>
    public static string GenerateKey() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
}

/// <summary>
/// İşlem çıktısındaki gizli değerleri <c>****</c> ile değiştirir. Çıktı parça parça geldiği için tam satırlar maskelenir;
/// yarım kalan satır sonraki parçayla birleştirilir, böylece parçalara bölünmüş bir parola da yakalanır.
/// </summary>
public sealed class SecretMasker
{
    public const string Mask = "****";

    private readonly string[] _secrets;
    private readonly StringBuilder _pending = new();
    private readonly Lock _gate = new();

    public SecretMasker(IEnumerable<string?> secrets)
    {
        // Çok kısa değerler sıradan çıktıyı bozacağı için maskelenmez; uzun olanlar önce değiştirilir.
        _secrets = secrets
            .Where(s => !string.IsNullOrEmpty(s) && s.Length >= 4)
            .Select(s => s!)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(s => s.Length)
            .ToArray();
    }

    public string Apply(string text)
    {
        if (string.IsNullOrEmpty(text) || _secrets.Length == 0)
            return text;

        var builder = new StringBuilder(text);
        foreach (var secret in _secrets)
        {
            builder.Replace(secret, Mask);
            var encoded = Uri.EscapeDataString(secret);
            if (!string.Equals(encoded, secret, StringComparison.Ordinal))
                builder.Replace(encoded, Mask);
        }

        return builder.ToString();
    }

    /// <summary>Gelen parçayı biriktirir ve tamamlanan satırları maskelenmiş olarak döner (yoksa boş metin).</summary>
    public string Push(string chunk)
    {
        if (_secrets.Length == 0)
            return chunk;

        lock (_gate)
        {
            _pending.Append(chunk);
            var text = _pending.ToString();
            var cut = text.LastIndexOf('\n');
            if (cut < 0)
            {
                // Satır sonu gelmeyen uzun çıktı (ilerleme çubukları) sonsuza kadar bekletilmez.
                if (text.Length < 4096)
                    return string.Empty;

                _pending.Clear();
                return Apply(text);
            }

            _pending.Clear();
            _pending.Append(text, cut + 1, text.Length - cut - 1);
            return Apply(text[..(cut + 1)]);
        }
    }

    /// <summary>Bekleyen yarım satırı maskelenmiş olarak döner.</summary>
    public string Flush()
    {
        lock (_gate)
        {
            var text = _pending.ToString();
            _pending.Clear();
            return Apply(text);
        }
    }
}
