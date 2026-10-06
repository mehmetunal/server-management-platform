namespace ServerManager.Application.Backups;

/// <summary>
/// Önceden okunmuş baytları, ardından alttaki akışın kalanını veren salt okunur akış. Kapatılınca alttaki akış da kapatılır.
/// Geri sarılamayan depolama akışında (S3, Azure) başı okunup doğrulandıktan sonra tamamını yeniden açmadan aktarmak için.
/// </summary>
public sealed class PrefixedReadStream : Stream
{
    private readonly byte[] _prefix;
    private readonly Stream _inner;
    private int _prefixPosition;

    public PrefixedReadStream(byte[] prefix, Stream inner)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(inner);
        _prefix = prefix;
        _inner = inner;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (TryReadPrefix(buffer, out var read))
            return read;
        return _inner.Read(buffer);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (TryReadPrefix(buffer.Span, out var read))
            return ValueTask.FromResult(read);
        return _inner.ReadAsync(buffer, cancellationToken);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        await base.DisposeAsync();
    }

    private bool TryReadPrefix(Span<byte> buffer, out int read)
    {
        read = 0;
        var remaining = _prefix.Length - _prefixPosition;
        if (remaining <= 0 || buffer.Length == 0)
            return false;

        read = Math.Min(remaining, buffer.Length);
        _prefix.AsSpan(_prefixPosition, read).CopyTo(buffer);
        _prefixPosition += read;
        return true;
    }
}
