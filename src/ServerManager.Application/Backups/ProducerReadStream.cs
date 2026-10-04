using System.IO.Pipelines;

namespace ServerManager.Application.Backups;

/// <summary>
/// Arka planda bir üreticinin yazdığı veriyi okunabilir akış olarak sunar. Okuyucu kapatılınca üretici iptal edilir;
/// üretici hata verirse okuma aynı hatayla biter.
/// </summary>
public sealed class ProducerReadStream : Stream
{
    private const long PauseWriterThreshold = 4 * 1024 * 1024;

    private readonly Pipe _pipe = new(new PipeOptions(pauseWriterThreshold: PauseWriterThreshold, resumeWriterThreshold: PauseWriterThreshold / 2, useSynchronizationContext: false));
    private readonly CancellationTokenSource _cts = new();
    private readonly Stream _reader;
    private readonly Task _producer;

    public ProducerReadStream(Func<Stream, CancellationToken, Task> produce)
    {
        _reader = _pipe.Reader.AsStream();
        _producer = Task.Run(async () =>
        {
            try
            {
                await using (var writer = _pipe.Writer.AsStream(leaveOpen: true))
                {
                    await produce(writer, _cts.Token);
                }
                await _pipe.Writer.CompleteAsync();
            }
            catch (Exception ex)
            {
                await _pipe.Writer.CompleteAsync(ex);
            }
        });
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

    public override int Read(byte[] buffer, int offset, int count) => _reader.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => _reader.Read(buffer);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _reader.ReadAsync(buffer, cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _reader.ReadAsync(buffer, offset, count, cancellationToken);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_cts.IsCancellationRequested)
            return;

        await _cts.CancelAsync();
        await _reader.DisposeAsync();
        try
        {
            await _producer;
        }
        catch (Exception)
        {
            // Üretici hatası okuyucuya zaten iletildi.
        }
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
