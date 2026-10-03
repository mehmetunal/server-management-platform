using System.Text;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Infrastructure.Ssh;

internal sealed class SshTerminalSession : ITerminalSession
{
    private const string WrongSudoPasswordMessage = "\r\n\x1b[31mSudo parolası hatalı. Sunucu ayarlarını kontrol edin.\x1b[0m\r\n";
    private static readonly TimeSpan SudoPromptWindow = TimeSpan.FromSeconds(15);

    private readonly SshClientLease _lease;
    private readonly ShellStream _stream;
    private readonly ITerminalOutputSink _sink;
    private readonly string? _sudoPassword;
    private readonly ILogger _logger;
    private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private int _sudoPromptCount;
    private string _pendingOutput = string.Empty;
    private int _closed;
    private long _lastActivityTicks;

    public SshTerminalSession(SshClientLease lease, ShellStream stream, ITerminalOutputSink sink, string? sudoPassword, ILogger logger)
    {
        _lease = lease;
        _stream = stream;
        _sink = sink;
        _sudoPassword = sudoPassword;
        _logger = logger;
        StartedAt = DateTime.UtcNow;
        _lastActivityTicks = StartedAt.Ticks;
    }

    public DateTime StartedAt { get; }

    public DateTime LastActivityAt => new(Interlocked.Read(ref _lastActivityTicks), DateTimeKind.Utc);

    public bool IsClosed => Volatile.Read(ref _closed) == 1;

    public void Start(string startupLine)
    {
        _ = Task.Factory.StartNew(ReadLoopAsync, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        WriteRaw(startupLine + "\n");
    }

    public async Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        if (IsClosed || string.IsNullOrEmpty(data))
            return;

        Touch();
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            WriteRaw(data);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or InvalidOperationException)
        {
            await CloseAsync("Bağlantı koptu.");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Resize(int columns, int rows)
    {
        if (IsClosed)
            return;

        try
        {
            _stream.ChangeWindowSize((uint)Math.Clamp(columns, 20, 500), (uint)Math.Clamp(rows, 5, 200), 0, 0);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // Oturum kapanırken gelen boyut değişikliği yok sayılır.
        }
    }

    public ValueTask DisposeAsync() => new(CloseAsync("Bağlantı kesildi."));

    private void WriteRaw(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Flush();
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[8192];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        string? reason = null;

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var read = _stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;

                Touch();
                var count = _decoder.GetChars(buffer, 0, read, chars, 0);
                var text = HandleSudoPrompt(new string(chars, 0, count));
                if (text is null)
                {
                    await _sink.OnOutputAsync(WrongSudoPasswordMessage);
                    reason = "Sudo parolası hatalı.";
                    break;
                }

                if (text.Length > 0)
                    await _sink.OnOutputAsync(text);
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or OperationCanceledException)
        {
            // Akış kapatıldığında okuma döngüsü sonlanır.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Terminal okuma döngüsü beklenmedik şekilde sonlandı.");
            reason = "Terminal bağlantısında hata oluştu.";
        }

        await CloseAsync(reason ?? "Oturum sonlandı.");
    }

    /// <returns>İletilecek metin; sudo parolası ikinci kez sorulduysa null.</returns>
    private string? HandleSudoPrompt(string text)
    {
        if (_sudoPassword is null)
            return text;

        const string marker = SudoCommandBuilder.TerminalPromptMarker;
        var buffer = _pendingOutput + text;
        _pendingOutput = string.Empty;

        var index = buffer.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            // İşaret iki okuma arasında bölünebilir; parola gönderilene kadar olası ön eki bir sonraki parçaya bekletir.
            if (Volatile.Read(ref _sudoPromptCount) == 0 && DateTime.UtcNow - StartedAt < SudoPromptWindow)
            {
                var keep = PartialMarkerSuffixLength(buffer, marker);
                _pendingOutput = buffer[^keep..];
                return buffer[..^keep];
            }

            return buffer;
        }

        if (Interlocked.Increment(ref _sudoPromptCount) > 1)
            return null;

        WriteRaw(_sudoPassword + "\n");
        return buffer.Remove(index, marker.Length);
    }

    private static int PartialMarkerSuffixLength(string buffer, string marker)
    {
        for (var length = Math.Min(marker.Length - 1, buffer.Length); length > 0; length--)
        {
            if (marker.AsSpan().StartsWith(buffer.AsSpan(buffer.Length - length), StringComparison.Ordinal))
                return length;
        }

        return 0;
    }

    private void Touch() => Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

    private async Task CloseAsync(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return;

        try
        {
            await _cts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            _stream.Dispose();
        }
        catch (Exception)
        {
            // Kanal zaten kapanmış olabilir.
        }

        _lease.Dispose();

        try
        {
            await _sink.OnClosedAsync(reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Terminal kapanış bildirimi gönderilemedi.");
        }
    }
}
