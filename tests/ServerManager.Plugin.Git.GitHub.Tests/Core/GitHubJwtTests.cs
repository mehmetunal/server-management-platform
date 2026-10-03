using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Tests.Fakes;

namespace ServerManager.Plugin.Git.GitHub.Tests.Core;

public class GitHubJwtTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Token_is_rs256_signed_with_app_claims()
    {
        var token = GitHubJwt.Create(123456, TestKeys.PrivateKeyPem, Now);

        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());

        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        Assert.Equal("123456", payload.RootElement.GetProperty("iss").GetString());
        Assert.Equal(Now.AddSeconds(-60).ToUnixTimeSeconds(), payload.RootElement.GetProperty("iat").GetInt64());
        var exp = payload.RootElement.GetProperty("exp").GetInt64();
        Assert.True(exp - Now.ToUnixTimeSeconds() <= 600, "GitHub 10 dakikadan uzun JWT kabul etmez.");

        using var rsa = TestKeys.CreateRsa();
        var valid = rsa.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            Base64Url.DecodeFromChars(parts[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        Assert.True(valid);
    }

    [Fact]
    public void Accepts_pkcs8_keys()
    {
        using var rsa = TestKeys.CreateRsa();
        var pkcs8 = rsa.ExportPkcs8PrivateKeyPem();

        Assert.True(GitHubJwt.IsValidPrivateKey(pkcs8));
        Assert.NotEmpty(GitHubJwt.Create(1, pkcs8, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a key")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nAAAA\n-----END RSA PRIVATE KEY-----")]
    public void Rejects_invalid_keys(string? pem) =>
        Assert.False(GitHubJwt.IsValidPrivateKey(pem));

    [Fact]
    public void Rejects_short_keys()
    {
        using var weak = RSA.Create(1024);

        Assert.False(GitHubJwt.IsValidPrivateKey(weak.ExportRSAPrivateKeyPem()));
    }
}
