using System.Text;
using ServerManager.Application.Interfaces.ManagedServices;

namespace ServerManager.Application.ManagedServices;

/// <summary>Arka planda süren servis işlemini başlatan kullanıcı; HTTP isteği bittikten sonra audit için kullanılır.</summary>
public sealed record ServiceActor(string? UserId, string? UserName, string? IpAddress);

/// <summary>
/// İşlem çıktısını maskeleyip canlı izleyiciye iletir ve kayıt için sınırlı boyutta biriktirir; log belirli aralıklarla
/// kaydedilir, böylece uygulama çökse bile son hali görülebilir.
/// </summary>
public sealed class ServiceOperationRecorder : IServiceOperationObserver
{
    private const string TruncatedNotice = "[… logun başı kısaltıldı …]\r\n";
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(10);

    private readonly IServiceOperationObserver _inner;
    private readonly SecretMasker _masker;
    private readonly Func<string, CancellationToken, Task> _flush;
    private readonly Func<ServiceOperationStage, CancellationToken, Task> _stageChanged;
    private readonly TimeProvider _timeProvider;
    private readonly StringBuilder _log = new();
    private readonly Lock _gate = new();
    private readonly int _maxChars;
    private bool _truncated;
    private DateTimeOffset _lastFlush;

    public ServiceOperationRecorder(
        IServiceOperationObserver inner,
        SecretMasker masker,
        int maxChars,
        Func<string, CancellationToken, Task> flush,
        Func<ServiceOperationStage, CancellationToken, Task> stageChanged,
        TimeProvider timeProvider)
    {
        _inner = inner;
        _masker = masker;
        _maxChars = Math.Max(4096, maxChars);
        _flush = flush;
        _stageChanged = stageChanged;
        _timeProvider = timeProvider;
        _lastFlush = timeProvider.GetUtcNow();
    }

    public ServiceOperationStage Stage { get; private set; } = ServiceOperationStage.Docker;

    public string Log
    {
        get
        {
            lock (_gate)
                return _truncated ? TruncatedNotice + _log : _log.ToString();
        }
    }

    public async Task OnOutputAsync(string text, CancellationToken cancellationToken)
    {
        var masked = _masker.Push(text);
        if (masked.Length == 0)
            return;

        await WriteAsync(masked, cancellationToken);
    }

    public async Task OnStageAsync(ServiceOperationStage stage, string message, CancellationToken cancellationToken)
    {
        if (stage is not ServiceOperationStage.Failed)
            Stage = stage;

        await FlushPendingAsync(cancellationToken);
        await _stageChanged(stage, cancellationToken);
        await WriteAsync(ServiceConsole.Step(message), cancellationToken);
        await _inner.OnStageAsync(stage, message, cancellationToken);
    }

    public Task InfoAsync(string message, CancellationToken cancellationToken) =>
        OnOutputAsync(ServiceConsole.Info(message), cancellationToken);

    /// <summary>Maskeleyicide bekleyen yarım satırı yazar (işlem sonunda).</summary>
    public async Task FlushPendingAsync(CancellationToken cancellationToken)
    {
        var rest = _masker.Flush();
        if (rest.Length > 0)
            await WriteAsync(rest, cancellationToken);
    }

    private async Task WriteAsync(string text, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _log.Append(text);
            if (_log.Length > _maxChars)
            {
                _log.Remove(0, _log.Length - _maxChars);
                _truncated = true;
            }
        }

        await _inner.OnOutputAsync(text, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        if (now - _lastFlush < FlushInterval)
            return;

        _lastFlush = now;
        await _flush(Log, cancellationToken);
    }
}

public sealed class NullServiceOperationObserver : IServiceOperationObserver
{
    public static readonly NullServiceOperationObserver Instance = new();

    public Task OnOutputAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnStageAsync(ServiceOperationStage stage, string message, CancellationToken cancellationToken) => Task.CompletedTask;
}
