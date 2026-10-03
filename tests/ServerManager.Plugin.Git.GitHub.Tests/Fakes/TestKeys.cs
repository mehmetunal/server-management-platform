using System.Security.Cryptography;

namespace ServerManager.Plugin.Git.GitHub.Tests.Fakes;

/// <summary>
/// Testler için çalışma anında üretilen RSA anahtarı; depoda anahtar dosyası tutulmaz. RSA nesneleri iş parçacığı
/// güvenli olmadığından paralel testler yalnızca PEM metnini paylaşır.
/// </summary>
public static class TestKeys
{
    private static readonly Lazy<string> Pem = new(() =>
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    });

    public static string PrivateKeyPem => Pem.Value;

    public static RSA CreateRsa()
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(Pem.Value);
        return rsa;
    }
}
