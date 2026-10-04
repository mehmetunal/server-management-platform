namespace ServerManager.Application.Tests.Backups;

/// <summary>Birkaç bayt verdikten sonra yarıda kesilen kaynak akışı.</summary>
public sealed class FailingStream : Stream
{
    private int _reads;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_reads++ > 0)
            throw new InvalidOperationException("Kaynak kesildi.");

        buffer[offset] = 42;
        return 1;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
