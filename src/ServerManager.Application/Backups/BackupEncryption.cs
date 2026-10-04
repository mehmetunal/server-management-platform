using System.Buffers.Binary;
using System.Security.Cryptography;

namespace ServerManager.Application.Backups;

/// <summary>
/// Yedek dosyası biçimi (SMBK v1). Dosya yalnızca parolayla, panel ve Security:MasterKey olmadan da çözülebilir
/// (bkz. tools/backup-decrypt.py):
/// <code>
/// başlık (36 bayt): "SMBK" | sürüm=1 | bayrak=1 (şifreli) | 2 bayt boş | PBKDF2 tur sayısı (uint32 BE) | salt (16) | nonce öneki (8)
/// anahtar        : PBKDF2-HMAC-SHA256(parola UTF-8, salt, tur, 32 bayt)
/// parça (tekrar) : son mu (1 bayt) | düz metin uzunluğu (uint32 BE, en fazla 1 MiB) | şifreli metin | GCM etiketi (16)
/// parça i nonce  : nonce öneki || i (uint32 BE);  AAD: başlık || i (uint32 BE) || son mu
/// </code>
/// Son parça işaretlidir; işaretli parça gelmeden biten (yarım kalmış) dosya reddedilir.
/// </summary>
public static class BackupEncryption
{
    public const string FileExtension = ".smbk";
    public const int DefaultIterations = 600_000;
    public const int MinIterations = 100_000;
    public const int MaxIterations = 10_000_000;
    public const int ChunkSize = 1024 * 1024;
    public const int HeaderLength = 36;

    private const byte Version = 1;
    private const byte EncryptedFlag = 1;
    private const int IterationsOffset = 8;
    private const int SaltOffset = 12;
    private const int SaltLength = 16;
    private const int NoncePrefixOffset = 28;
    private const int NoncePrefixLength = 8;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32;
    private const int RecordHeaderLength = 5;

    private static ReadOnlySpan<byte> Magic => "SMBK"u8;

    public static async Task EncryptAsync(Stream input, Stream output, string passphrase, int iterations, CancellationToken cancellationToken = default)
    {
        if (iterations is < MinIterations or > MaxIterations)
            throw new ArgumentOutOfRangeException(nameof(iterations));

        var header = new byte[HeaderLength];
        Magic.CopyTo(header);
        header[4] = Version;
        header[5] = EncryptedFlag;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(IterationsOffset), (uint)iterations);
        RandomNumberGenerator.Fill(header.AsSpan(SaltOffset, SaltLength));
        RandomNumberGenerator.Fill(header.AsSpan(NoncePrefixOffset, NoncePrefixLength));

        var key = DeriveKey(passphrase, header.AsSpan(SaltOffset, SaltLength), iterations);
        try
        {
            using var aes = new AesGcm(key, TagLength);
            await output.WriteAsync(header, cancellationToken);

            var current = new byte[ChunkSize];
            var next = new byte[ChunkSize];
            var cipher = new byte[ChunkSize];
            var record = new byte[RecordHeaderLength];
            var tag = new byte[TagLength];
            var nonce = new byte[NonceLength];
            var aad = new byte[HeaderLength + 5];
            header.CopyTo(aad, 0);

            var length = await input.ReadAtLeastAsync(current, ChunkSize, throwOnEndOfStream: false, cancellationToken);
            for (uint counter = 0; ; counter++)
            {
                var nextLength = length == ChunkSize
                    ? await input.ReadAtLeastAsync(next, ChunkSize, throwOnEndOfStream: false, cancellationToken)
                    : 0;
                var isFinal = nextLength == 0;

                FillChunkParameters(header, counter, isFinal, nonce, aad);
                aes.Encrypt(nonce, current.AsSpan(0, length), cipher.AsSpan(0, length), tag, aad);

                record[0] = isFinal ? (byte)1 : (byte)0;
                BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(1), (uint)length);
                await output.WriteAsync(record, cancellationToken);
                await output.WriteAsync(cipher.AsMemory(0, length), cancellationToken);
                await output.WriteAsync(tag, cancellationToken);

                if (isFinal)
                    break;
                if (counter == uint.MaxValue)
                    throw new InvalidOperationException("Yedek dosyası çok büyük.");

                (current, next) = (next, current);
                length = nextLength;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <exception cref="BackupFormatException">Parola yanlış, dosya bozuk veya yarım kalmış.</exception>
    public static async Task DecryptAsync(Stream input, Stream output, string passphrase, CancellationToken cancellationToken = default)
    {
        var header = new byte[HeaderLength];
        if (await input.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken) < HeaderLength
            || !header.AsSpan(0, 4).SequenceEqual(Magic))
            throw new BackupFormatException("Dosya şifreli bir Server Manager yedeği değil.");
        if (header[4] != Version || header[5] != EncryptedFlag)
            throw new BackupFormatException("Yedek dosyası sürümü desteklenmiyor.");

        var iterations = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(IterationsOffset));
        if (iterations is < MinIterations or > MaxIterations)
            throw new BackupFormatException("Yedek dosyasının başlığı geçersiz.");

        var key = DeriveKey(passphrase, header.AsSpan(SaltOffset, SaltLength), (int)iterations);
        try
        {
            using var aes = new AesGcm(key, TagLength);
            var record = new byte[RecordHeaderLength];
            var cipher = new byte[ChunkSize + TagLength];
            var plain = new byte[ChunkSize];
            var nonce = new byte[NonceLength];
            var aad = new byte[HeaderLength + 5];
            header.CopyTo(aad, 0);

            for (uint counter = 0; ; counter++)
            {
                if (await input.ReadAtLeastAsync(record, RecordHeaderLength, throwOnEndOfStream: false, cancellationToken) < RecordHeaderLength)
                    throw new BackupFormatException("Yedek dosyası eksik (yarım kalmış).");

                var isFinal = record[0] switch
                {
                    0 => false,
                    1 => true,
                    _ => throw new BackupFormatException("Yedek dosyası bozuk.")
                };
                var length = BinaryPrimitives.ReadUInt32BigEndian(record.AsSpan(1));
                if (length > ChunkSize)
                    throw new BackupFormatException("Yedek dosyası bozuk.");

                var total = (int)length + TagLength;
                if (await input.ReadAtLeastAsync(cipher.AsMemory(0, total), total, throwOnEndOfStream: false, cancellationToken) < total)
                    throw new BackupFormatException("Yedek dosyası eksik (yarım kalmış).");

                FillChunkParameters(header, counter, isFinal, nonce, aad);
                try
                {
                    aes.Decrypt(nonce, cipher.AsSpan(0, (int)length), cipher.AsSpan((int)length, TagLength), plain.AsSpan(0, (int)length), aad);
                }
                catch (AuthenticationTagMismatchException ex)
                {
                    throw new BackupFormatException(counter == 0
                        ? "Şifre çözülemedi: parola yanlış veya dosya bozuk."
                        : "Yedek dosyası bozuk (doğrulama başarısız).", ex);
                }

                await output.WriteAsync(plain.AsMemory(0, (int)length), cancellationToken);
                if (!isFinal)
                    continue;

                if (await input.ReadAsync(record.AsMemory(0, 1), cancellationToken) > 0)
                    throw new BackupFormatException("Yedek dosyasının sonunda beklenmeyen veri var.");
                return;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DeriveKey(string passphrase, ReadOnlySpan<byte> salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, HashAlgorithmName.SHA256, KeyLength);

    private static void FillChunkParameters(byte[] header, uint counter, bool isFinal, byte[] nonce, byte[] aad)
    {
        header.AsSpan(NoncePrefixOffset, NoncePrefixLength).CopyTo(nonce);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NoncePrefixLength), counter);
        BinaryPrimitives.WriteUInt32BigEndian(aad.AsSpan(HeaderLength), counter);
        aad[HeaderLength + 4] = isFinal ? (byte)1 : (byte)0;
    }
}
