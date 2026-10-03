using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServerManager.Plugin.Git.GitHub.Core;

/// <summary>GitHub App kimliğiyle API çağırmak için RS256 imzalı kısa ömürlü JWT.</summary>
public static class GitHubJwt
{
    private const int MinKeySizeBits = 2048;
    private static readonly byte[] Header = Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}""");

    /// <summary>GitHub en fazla 10 dakika kabul eder; saat kaymasına karşı iat 60 sn geriye alınır.</summary>
    public static string Create(long appId, string privateKeyPem, DateTimeOffset now)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["iat"] = now.AddSeconds(-60).ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(9).ToUnixTimeSeconds(),
            ["iss"] = appId.ToString(CultureInfo.InvariantCulture)
        });

        var signingInput = $"{Base64Url.EncodeToString(Header)}.{Base64Url.EncodeToString(payload)}";
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    public static bool IsValidPrivateKey(string? privateKeyPem)
    {
        if (string.IsNullOrWhiteSpace(privateKeyPem))
            return false;

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            return rsa.KeySize >= MinKeySizeBits;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
