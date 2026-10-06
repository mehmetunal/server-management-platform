using System.Security.Cryptography;
using System.Text;

namespace ServerManager.Application.ApiKeys;

/// <summary>Oluşturulan anahtarın kullanıcıya bir kez gösterilen tam değeri ve saklanan parçaları.</summary>
public sealed record GeneratedApiKey(string Token, string Prefix, string Hash);

/// <summary>
/// API anahtarı biçimi: <c>smk_&lt;önek&gt;_&lt;gizli&gt;</c>. Önek 12 küçük harf/rakamdır, kayıt aramak ve listede anahtarı
/// tanımak için açık saklanır. Gizli kısım 43 harf/rakamdır (~256 bit). Veritabanında yalnızca tam değerin SHA-256 özeti
/// tutulur; doğrulama sabit zamanlı karşılaştırmayla yapılır.
/// </summary>
public static class ApiKeyToken
{
    public const string Scheme = "smk";
    public const int PrefixLength = 12;
    public const int SecretLength = 43;

    private const string PrefixAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const string SecretAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static int TokenLength => Scheme.Length + 1 + PrefixLength + 1 + SecretLength;

    public static GeneratedApiKey Generate()
    {
        var prefix = RandomNumberGenerator.GetString(PrefixAlphabet, PrefixLength);
        var secret = RandomNumberGenerator.GetString(SecretAlphabet, SecretLength);
        var token = $"{Scheme}_{prefix}_{secret}";
        return new GeneratedApiKey(token, prefix, ComputeHash(token));
    }

    /// <summary>Biçim geçerliyse öneki döner; değilse false (veritabanına hiç gidilmez).</summary>
    public static bool TryParse(string? token, out string prefix)
    {
        prefix = string.Empty;
        if (token is null || token.Length != TokenLength)
            return false;

        if (!token.StartsWith(Scheme + "_", StringComparison.Ordinal) || token[Scheme.Length + 1 + PrefixLength] != '_')
            return false;

        var candidatePrefix = token.Substring(Scheme.Length + 1, PrefixLength);
        var secret = token[(Scheme.Length + 1 + PrefixLength + 1)..];
        if (!candidatePrefix.All(c => PrefixAlphabet.Contains(c)) || !secret.All(char.IsAsciiLetterOrDigit))
            return false;

        prefix = candidatePrefix;
        return true;
    }

    /// <summary>Tam anahtarın SHA-256 özeti (küçük harf hex, 64 karakter).</summary>
    public static string ComputeHash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Anahtarın saklanan özetle eşleşip eşleşmediği; sabit zamanlı karşılaştırma.</summary>
    public static bool Verify(string token, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash) || storedHash.Length != 64)
            return false;

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Listede gösterilen kısaltılmış biçim: <c>smk_abcd1234efgh_…</c>.</summary>
    public static string Display(string prefix) => $"{Scheme}_{prefix}_…";
}
