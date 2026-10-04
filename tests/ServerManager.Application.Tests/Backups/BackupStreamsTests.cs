using System.Security.Cryptography;
using System.Text;
using ServerManager.Application.Backups;

namespace ServerManager.Application.Tests.Backups;

public class BackupStreamsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hashing_stream_counts_bytes_and_hashes_what_was_read()
    {
        var data = Encoding.UTF8.GetBytes("server-manager");
        await using var hashing = new HashingStream(new MemoryStream(data));
        using var copy = new MemoryStream();

        await hashing.CopyToAsync(copy, Ct);

        Assert.Equal(data.Length, hashing.BytesTransferred);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(data)), hashing.Sha256Hex);
    }

    [Fact]
    public async Task Hash_is_available_after_the_stream_is_disposed()
    {
        var data = Encoding.UTF8.GetBytes("upload");
        var hashing = new HashingStream(new MemoryStream(data));
        await hashing.CopyToAsync(Stream.Null, Ct);

        await hashing.DisposeAsync();

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(data)), hashing.Sha256Hex);
        Assert.Equal(data.Length, hashing.BytesTransferred);
    }

    [Fact]
    public async Task Producer_stream_delivers_everything_the_producer_wrote()
    {
        var data = RandomNumberGenerator.GetBytes(10 * 1024 * 1024 + 3);
        await using var stream = new ProducerReadStream(async (output, token) => await output.WriteAsync(data, token));
        using var copy = new MemoryStream();

        await stream.CopyToAsync(copy, Ct);

        Assert.Equal(data, copy.ToArray());
    }

    [Fact]
    public async Task Producer_error_surfaces_to_the_reader()
    {
        await using var stream = new ProducerReadStream(async (output, token) =>
        {
            await output.WriteAsync(new byte[] { 1, 2, 3 }, token);
            throw new BackupFormatException("bozuk");
        });

        await Assert.ThrowsAsync<BackupFormatException>(() => stream.CopyToAsync(Stream.Null, Ct));
    }

    [Fact]
    public async Task Disposing_the_reader_cancels_the_producer()
    {
        var cancelled = new TaskCompletionSource();
        var stream = new ProducerReadStream(async (output, token) =>
        {
            try
            {
                var chunk = new byte[64 * 1024];
                while (true)
                    await output.WriteAsync(chunk, token);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
        });

        await stream.ReadExactlyAsync(new byte[16], Ct);
        await stream.DisposeAsync();

        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
    }

    [Fact]
    public async Task Decrypting_producer_round_trips_encrypted_data()
    {
        var plain = RandomNumberGenerator.GetBytes(3 * 1024 * 1024);
        using var encrypted = new MemoryStream();
        await BackupEncryption.EncryptAsync(new MemoryStream(plain), encrypted, "parola-123456", BackupEncryption.MinIterations, Ct);
        encrypted.Position = 0;

        await using var stream = new ProducerReadStream((output, token) => BackupEncryption.DecryptAsync(encrypted, output, "parola-123456", token));
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, Ct);

        Assert.Equal(plain, copy.ToArray());
    }
}
