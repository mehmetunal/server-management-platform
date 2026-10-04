using System.Security.Cryptography;
using ServerManager.Application.Backups;

namespace ServerManager.Application.Tests.Backups;

public class BackupEncryptionTests
{
    private const string Passphrase = "doğru-parola-123";
    private const int Iterations = BackupEncryption.MinIterations;

    private static async Task<byte[]> EncryptAsync(byte[] plain, string passphrase = Passphrase)
    {
        using var output = new MemoryStream();
        await BackupEncryption.EncryptAsync(new MemoryStream(plain), output, passphrase, Iterations, TestContext.Current.CancellationToken);
        return output.ToArray();
    }

    private static async Task<byte[]> DecryptAsync(byte[] encrypted, string passphrase = Passphrase)
    {
        using var output = new MemoryStream();
        await BackupEncryption.DecryptAsync(new MemoryStream(encrypted), output, passphrase, TestContext.Current.CancellationToken);
        return output.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(BackupEncryption.ChunkSize - 1)]
    [InlineData(BackupEncryption.ChunkSize)]
    [InlineData(BackupEncryption.ChunkSize * 2 + 17)]
    public async Task Round_trip_restores_the_original_bytes(int length)
    {
        var plain = RandomNumberGenerator.GetBytes(length);

        var encrypted = await EncryptAsync(plain);

        Assert.Equal("SMBK"u8.ToArray(), encrypted[..4]);
        Assert.Equal(plain, await DecryptAsync(encrypted));
    }

    [Fact]
    public async Task Same_input_produces_different_ciphertext_each_time()
    {
        var plain = RandomNumberGenerator.GetBytes(1000);

        Assert.NotEqual(await EncryptAsync(plain), await EncryptAsync(plain));
    }

    [Fact]
    public async Task Wrong_passphrase_is_rejected()
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));

        var ex = await Assert.ThrowsAsync<BackupFormatException>(() => DecryptAsync(encrypted, "yanlış-parola-123"));
        Assert.Contains("parola yanlış", ex.Message);
    }

    [Fact]
    public async Task Truncated_file_is_rejected_even_at_a_chunk_boundary()
    {
        var plain = RandomNumberGenerator.GetBytes(BackupEncryption.ChunkSize * 2 + 10);
        var encrypted = await EncryptAsync(plain);
        var firstChunkEnd = BackupEncryption.HeaderLength + 5 + BackupEncryption.ChunkSize + 16;

        await Assert.ThrowsAsync<BackupFormatException>(() => DecryptAsync(encrypted[..firstChunkEnd]));
        await Assert.ThrowsAsync<BackupFormatException>(() => DecryptAsync(encrypted[..^1]));
    }

    [Fact]
    public async Task Tampered_byte_is_detected()
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(BackupEncryption.ChunkSize + 100));
        encrypted[^20] ^= 0x01;

        await Assert.ThrowsAsync<BackupFormatException>(() => DecryptAsync(encrypted));
    }

    [Fact]
    public async Task Tampered_header_is_detected()
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));
        encrypted[BackupEncryption.HeaderLength - 1] ^= 0x01;

        await Assert.ThrowsAsync<BackupFormatException>(() => DecryptAsync(encrypted));
    }

    [Fact]
    public async Task Clearing_the_final_flag_is_detected()
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));
        encrypted[BackupEncryption.HeaderLength] = 0;

        await Assert.ThrowsAsync<BackupFormatException>(() => DecryptAsync(encrypted));
    }

    [Fact]
    public async Task Trailing_data_is_rejected()
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));

        await Assert.ThrowsAsync<BackupFormatException>(() => DecryptAsync([.. encrypted, 0x00]));
    }

    [Fact]
    public async Task Plain_gzip_file_is_not_accepted_as_encrypted()
    {
        var ex = await Assert.ThrowsAsync<BackupFormatException>(() => DecryptAsync([0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00]));
        Assert.Contains("şifreli", ex.Message);
    }

    [Theory]
    [InlineData(BackupEncryption.MinIterations - 1)]
    [InlineData(BackupEncryption.MaxIterations + 1)]
    public async Task Iteration_count_outside_limits_is_refused(int iterations)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            BackupEncryption.EncryptAsync(new MemoryStream([1]), new MemoryStream(), Passphrase, iterations, TestContext.Current.CancellationToken));
    }
}
