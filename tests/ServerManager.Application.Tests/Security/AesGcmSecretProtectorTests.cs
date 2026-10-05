using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ServerManager.Infrastructure.Security;

namespace ServerManager.Application.Tests.Security;

public class AesGcmSecretProtectorTests
{
    private static AesGcmSecretProtector CreateProtector(byte[]? key = null, int keyVersion = 1) =>
        new(Options.Create(new SecurityOptions
        {
            MasterKey = Convert.ToBase64String(key ?? RandomNumberGenerator.GetBytes(32)),
            KeyVersion = keyVersion
        }));

    [Theory]
    [InlineData("S3cret-pass!")]
    [InlineData("çğıöşü ÇĞİÖŞÜ")]
    [InlineData("")]
    public void Protect_then_unprotect_round_trips(string plaintext)
    {
        var protector = CreateProtector();

        var protectedValue = protector.Protect(plaintext);

        Assert.Equal(plaintext, protector.Unprotect(protectedValue));
    }

    [Fact]
    public void Protected_value_has_version_prefix_and_hides_plaintext()
    {
        var protector = CreateProtector(keyVersion: 3);

        var protectedValue = protector.Protect("my-password");

        Assert.StartsWith("v3:", protectedValue);
        Assert.DoesNotContain("my-password", protectedValue);
    }

    [Fact]
    public void Same_plaintext_produces_different_ciphertexts()
    {
        var protector = CreateProtector();

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        var protector = CreateProtector();
        var protectedValue = protector.Protect("my-password");
        var payload = Convert.FromBase64String(protectedValue[3..]);
        payload[^1] ^= 0x01;
        var tampered = "v1:" + Convert.ToBase64String(payload);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(tampered));
    }

    [Fact]
    public void Different_key_cannot_decrypt()
    {
        var protectedValue = CreateProtector().Protect("my-password");

        Assert.ThrowsAny<CryptographicException>(() => CreateProtector().Unprotect(protectedValue));
    }

    [Theory]
    [InlineData("plain-text")]
    [InlineData("v1:not-base64!!")]
    [InlineData("v1:AAAA")]
    public void Malformed_value_is_rejected(string value)
    {
        Assert.ThrowsAny<CryptographicException>(() => CreateProtector().Unprotect(value));
    }

    [Fact]
    public void Value_from_newer_key_version_is_rejected_with_clear_error()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var protectedValue = CreateProtector(key, keyVersion: 2).Protect("my-password");

        var ex = Assert.ThrowsAny<CryptographicException>(() => CreateProtector(key, keyVersion: 1).Unprotect(protectedValue));

        Assert.Contains("v2", ex.Message);
    }

    [Fact]
    public void Value_from_older_key_version_still_decrypts_with_same_key()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var protectedValue = CreateProtector(key, keyVersion: 1).Protect("my-password");

        Assert.Equal("my-password", CreateProtector(key, keyVersion: 2).Unprotect(protectedValue));
    }

    [Theory]
    [InlineData("vx:AAAA")]
    [InlineData("v0:AAAA")]
    [InlineData("v-1:AAAA")]
    public void Unknown_version_prefix_is_rejected(string value)
    {
        Assert.ThrowsAny<CryptographicException>(() => CreateProtector().Unprotect(value));
    }

    [Fact]
    public void Invalid_master_key_length_throws()
    {
        Assert.Throws<InvalidOperationException>(() => CreateProtector(RandomNumberGenerator.GetBytes(16)));
    }
}
