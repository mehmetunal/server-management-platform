using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ServerManager.Application.Interfaces.Security;

namespace ServerManager.Infrastructure.Security;

public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string VersionPrefix = "v";

    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("ServerManager.Secret");

    private readonly byte[] _key;

    public AesGcmSecretProtector(IOptions<SecurityOptions> options)
    {
        var validation = new SecurityOptionsValidator().Validate(null, options.Value);
        if (validation.Failed)
            throw new InvalidOperationException(validation.FailureMessage);

        _key = Convert.FromBase64String(options.Value.MasterKey);
        KeyVersion = options.Value.KeyVersion;
    }

    public int KeyVersion { get; }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceSize + TagSize + plainBytes.Length];
        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag, AssociatedData);

        CryptographicOperations.ZeroMemory(plainBytes);
        return $"{VersionPrefix}{KeyVersion}:{Convert.ToBase64String(payload)}";
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedValue);

        var separatorIndex = protectedValue.IndexOf(':');
        if (separatorIndex <= VersionPrefix.Length || !protectedValue.StartsWith(VersionPrefix, StringComparison.Ordinal))
            throw new CryptographicException("Şifreli değer formatı geçersiz.");

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(protectedValue[(separatorIndex + 1)..]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Şifreli değer formatı geçersiz.", ex);
        }

        if (payload.Length < NonceSize + TagSize)
            throw new CryptographicException("Şifreli değer formatı geçersiz.");

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plainBytes, AssociatedData);

        var plaintext = Encoding.UTF8.GetString(plainBytes);
        CryptographicOperations.ZeroMemory(plainBytes);
        return plaintext;
    }
}
