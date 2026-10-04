using Amazon.S3;
using Amazon.S3.Model;

namespace ServerManager.Plugin.Storage.S3;

/// <summary>S3 nesne akışı; kapatılınca yanıt ve istemci de kapatılır.</summary>
public sealed class S3ObjectStream : Stream
{
    private readonly GetObjectResponse _response;
    private readonly IAmazonS3 _client;
    private readonly Stream _inner;

    public S3ObjectStream(GetObjectResponse response, IAmazonS3 client)
    {
        _response = response;
        _client = client;
        _inner = response.ResponseStream;
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

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(buffer, cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _response.Dispose();
            _client.Dispose();
        }
        base.Dispose(disposing);
    }
}
