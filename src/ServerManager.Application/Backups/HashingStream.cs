using System.Security.Cryptography;

namespace ServerManager.Application.Backups;

/// <summary>İçinden geçen veriyi (okuma veya yazma yönünde) sayar ve SHA-256 özetini hesaplar.</summary>
public sealed class HashingStream : Stream
{
    private readonly Stream _inner;
    private readonly bool _leaveOpen;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private string? _hex;

    public HashingStream(Stream inner, bool leaveOpen = false)
    {
        _inner = inner;
        _leaveOpen = leaveOpen;
    }

    public long BytesTransferred { get; private set; }

    /// <summary>Akış bittikten sonra bir kez hesaplanır; sonrasında veri eklenmez.</summary>
    public string Sha256Hex => _hex ??= Convert.ToHexStringLower(_hash.GetHashAndReset());

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => _inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => BytesTransferred;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var read = _inner.Read(buffer);
        Append(buffer[..read]);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        Append(buffer.Span[..read]);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        Append(buffer);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken);
        Append(buffer.Span);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
            if (!_leaveOpen)
                _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        _hash.Dispose();
        if (!_leaveOpen)
            await _inner.DisposeAsync();

        await base.DisposeAsync();
    }

    private void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        _hash.AppendData(data);
        BytesTransferred += data.Length;
    }
}
