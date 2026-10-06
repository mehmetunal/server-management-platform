using System.Security.Cryptography;
using ServerManager.Application.Backups;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Backups;

public class BackupDownloadsTests
{
    private const string Passphrase = "indirme-parolasi-123";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Successful_backup_with_file_is_downloadable() =>
        Assert.Null(BackupDownloads.UnavailableReason(BackupOperation.Backup, BackupRunStatus.Succeeded, null, null, hasObjectKey: true));

    [Theory]
    [InlineData(BackupOperation.Restore, BackupRunStatus.Succeeded, false, true, "Geri yükleme")]
    [InlineData(BackupOperation.Backup, BackupRunStatus.Running, false, true, "sürüyor")]
    [InlineData(BackupOperation.Backup, BackupRunStatus.Failed, false, true, "başarılı")]
    [InlineData(BackupOperation.Backup, BackupRunStatus.Cancelled, false, true, "başarılı")]
    [InlineData(BackupOperation.Backup, BackupRunStatus.Interrupted, false, true, "başarılı")]
    [InlineData(BackupOperation.Backup, BackupRunStatus.Succeeded, true, true, "silinmiş")]
    [InlineData(BackupOperation.Backup, BackupRunStatus.Succeeded, false, false, "dosya kaydı")]
    public void Unavailable_runs_explain_why(BackupOperation operation, BackupRunStatus status, bool deleted, bool hasObjectKey, string expected)
    {
        var reason = BackupDownloads.UnavailableReason(operation, status, deleted ? DateTime.UtcNow : null, null, hasObjectKey);

        Assert.NotNull(reason);
        Assert.Contains(expected, reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Retention_deleted_reason_names_who_deleted_it()
    {
        var reason = BackupDownloads.UnavailableReason(BackupOperation.Backup, BackupRunStatus.Succeeded, DateTime.UtcNow, "Saklama politikası", true);

        Assert.Contains("Saklama politikası", reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("site-20261005-120000.tar.gz", "site-20261005-120000.tar.gz")]
    [InlineData("db-20261005-120000.sql.gz.smbk", "db-20261005-120000.sql.gz.smbk")]
    [InlineData("../../etc/passwd", "etc-passwd")]
    [InlineData("a\"b\r\nc;d.tar.gz", "a-b--c-d.tar.gz")]
    [InlineData("yedek dosyası.tar.gz", "yedek-dosyas-.tar.gz")]
    [InlineData(".hidden.tar.gz", "hidden.tar.gz")]
    public void File_name_is_sanitized(string input, string expected) =>
        Assert.Equal(expected, BackupDownloads.SafeFileName(input, null, Guid.NewGuid()));

    [Fact]
    public void Object_key_is_used_when_file_name_is_missing() =>
        Assert.Equal("20261005-120000-ab12cd34.tar.gz",
            BackupDownloads.SafeFileName(null, $"{Guid.NewGuid():N}/20261005-120000-ab12cd34.tar.gz", Guid.NewGuid()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("...")]
    [InlineData("////")]
    public void Empty_or_unsafe_name_falls_back_to_run_id(string? input)
    {
        var runId = Guid.NewGuid();

        Assert.Equal($"yedek-{runId.ToString("N")[..8]}.bin", BackupDownloads.SafeFileName(input, null, runId));
    }

    [Fact]
    public void Long_name_is_trimmed_from_the_start_keeping_the_extension()
    {
        var name = new string('a', 400) + ".sql.gz.smbk";

        var safe = BackupDownloads.SafeFileName(name, null, Guid.NewGuid());

        Assert.Equal(BackupDownloads.MaxFileNameLength, safe.Length);
        Assert.EndsWith(".sql.gz.smbk", safe, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a.tar.gz", "application/gzip")]
    [InlineData("a.sql.gz.smbk", "application/octet-stream")]
    [InlineData("a.bin", "application/octet-stream")]
    public void Content_type_follows_extension(string fileName, string expected) =>
        Assert.Equal(expected, BackupDownloads.ContentType(fileName));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(BackupEncryption.ChunkSize - 1)]
    [InlineData(BackupEncryption.ChunkSize)]
    [InlineData(BackupEncryption.ChunkSize + 1)]
    [InlineData(BackupEncryption.ChunkSize * 2)]
    [InlineData(BackupEncryption.ChunkSize * 3 + 5)]
    public async Task Plaintext_length_matches_encrypted_size(int length)
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(length));

        Assert.Equal(length, BackupEncryption.PlaintextLength(encrypted.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(BackupEncryption.HeaderLength + 20)]
    public void Plaintext_length_of_invalid_size_is_null(long encryptedLength) =>
        Assert.Null(BackupEncryption.PlaintextLength(encryptedLength));

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(BackupEncryption.ChunkSize + 10)]
    public async Task Verified_stream_decrypts_to_the_original_bytes(int length)
    {
        var plain = RandomNumberGenerator.GetBytes(length);
        var encrypted = await EncryptAsync(plain);

        await using var verified = await BackupEncryption.VerifyPassphraseAsync(new NonSeekableStream(encrypted), Passphrase, Ct);
        using var output = new MemoryStream();
        await BackupEncryption.DecryptAsync(verified, output, Passphrase, Ct);

        Assert.Equal(plain, output.ToArray());
    }

    [Fact]
    public async Task Verification_rejects_wrong_passphrase_before_any_output()
    {
        var encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));

        var ex = await Assert.ThrowsAsync<BackupFormatException>(() =>
            BackupEncryption.VerifyPassphraseAsync(new MemoryStream(encrypted), "yanlis-parola-000", Ct));

        Assert.Contains("parola yanlış", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verification_rejects_unencrypted_file() =>
        await Assert.ThrowsAsync<BackupFormatException>(() =>
            BackupEncryption.VerifyPassphraseAsync(new MemoryStream(new byte[200]), Passphrase, Ct));

    [Fact]
    public async Task Prefixed_stream_returns_prefix_then_rest_and_disposes_inner()
    {
        var inner = new NonSeekableStream([4, 5, 6]);
        var stream = new PrefixedReadStream([1, 2, 3], inner);
        using var output = new MemoryStream();

        await stream.CopyToAsync(output, 2, Ct);
        await stream.DisposeAsync();

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, output.ToArray());
        Assert.True(inner.IsDisposed);
    }

    private static async Task<byte[]> EncryptAsync(byte[] plain)
    {
        using var output = new MemoryStream();
        await BackupEncryption.EncryptAsync(new MemoryStream(plain), output, Passphrase, BackupEncryption.MinIterations, Ct);
        return output.ToArray();
    }

    /// <summary>S3/Azure akışı gibi geri sarılamayan okuma akışı.</summary>
    internal sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public bool IsDisposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
