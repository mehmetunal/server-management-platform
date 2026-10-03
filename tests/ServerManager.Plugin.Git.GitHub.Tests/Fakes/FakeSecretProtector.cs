using System.Security.Cryptography;
using ServerManager.Application.Interfaces.Security;

namespace ServerManager.Plugin.Git.GitHub.Tests.Fakes;

public sealed class FakeSecretProtector : ISecretProtector
{
    private const string Prefix = "enc:";

    public int KeyVersion { get; set; } = 1;

    public string Protect(string plaintext) => Prefix + new string(plaintext.Reverse().ToArray());

    public string Unprotect(string protectedValue)
    {
        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
            throw new CryptographicException("Çözülemedi.");

        return new string(protectedValue[Prefix.Length..].Reverse().ToArray());
    }
}
