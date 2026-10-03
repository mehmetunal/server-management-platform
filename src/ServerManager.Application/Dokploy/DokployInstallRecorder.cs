using ServerManager.Application.Interfaces.Dokploy;

namespace ServerManager.Application.Dokploy;

/// <summary>
/// Kurulum çıktısını hem canlı izleyiciye iletir hem de kayıt için biriktirir.
/// Uzun kurulumlarda çıktı belirli aralıklarla kaydedilir; uygulama çökerse bile son hal görülebilir.
/// </summary>
public sealed class DokployInstallRecorder : IDokployInstallObserver
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(10);

    private readonly IDokployInstallObserver _inner;
    private readonly Func<string, CancellationToken, Task> _flush;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastFlush;

    public DokployInstallRecorder(IDokployInstallObserver inner, int maxChars, Func<string, CancellationToken, Task> flush, TimeProvider timeProvider)
    {
        _inner = inner;
        _flush = flush;
        _timeProvider = timeProvider;
        _lastFlush = timeProvider.GetUtcNow();
        Log = new DokployInstallLog(maxChars);
    }

    public DokployInstallLog Log { get; }

    public async Task OnOutputAsync(string text, CancellationToken cancellationToken)
    {
        Log.Append(text);
        await _inner.OnOutputAsync(text, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        if (now - _lastFlush < FlushInterval)
            return;

        _lastFlush = now;
        await _flush(Log.ToString(), cancellationToken);
    }

    public Task OnStageAsync(DokployInstallStage stage, string message, CancellationToken cancellationToken) =>
        _inner.OnStageAsync(stage, message, cancellationToken);

    /// <summary>Panelin kendi bilgilendirme satırı; terminalde betik çıktısından ayırt edilsin diye renklidir.</summary>
    public Task InfoAsync(string message, CancellationToken cancellationToken) =>
        OnOutputAsync(DokployConsole.Info(message), cancellationToken);
}
