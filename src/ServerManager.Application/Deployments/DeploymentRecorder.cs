using ServerManager.Application.Interfaces.Deployments;

namespace ServerManager.Application.Deployments;

/// <summary>
/// Çıktıyı canlı izleyiciye iletir ve kayıt için biriktirir. Uzun deployment'larda log belirli aralıklarla kaydedilir;
/// uygulama çökerse bile son hal görülebilir. Aşama değişiklikleri ve alınan commit ayrıca kalıcı duruma yansıtılır;
/// böylece iptal edilen veya yarıda kalan deployment da hangi commit'te olduğunu bilir.
/// </summary>
public sealed class DeploymentRecorder : IDeploymentObserver
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(10);

    private readonly IDeploymentObserver _inner;
    private readonly Func<string, CancellationToken, Task> _flush;
    private readonly Func<DeploymentStage, CancellationToken, Task> _stageChanged;
    private readonly Func<DeploymentCommit, CancellationToken, Task> _commitResolved;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastFlush;

    public DeploymentRecorder(
        IDeploymentObserver inner,
        int maxChars,
        Func<string, CancellationToken, Task> flush,
        Func<DeploymentStage, CancellationToken, Task> stageChanged,
        Func<DeploymentCommit, CancellationToken, Task> commitResolved,
        TimeProvider timeProvider)
    {
        _inner = inner;
        _flush = flush;
        _stageChanged = stageChanged;
        _commitResolved = commitResolved;
        _timeProvider = timeProvider;
        _lastFlush = timeProvider.GetUtcNow();
        Log = new DeploymentLog(maxChars);
    }

    public DeploymentLog Log { get; }

    public DeploymentStage Stage { get; private set; } = DeploymentStage.Preparing;

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

    public async Task OnStageAsync(DeploymentStage stage, string message, CancellationToken cancellationToken)
    {
        Stage = stage;
        await _stageChanged(stage, cancellationToken);
        await OnOutputAsync(DeploymentConsole.Step(message), cancellationToken);
        await _inner.OnStageAsync(stage, message, cancellationToken);
    }

    public async Task OnCommitAsync(DeploymentCommit commit, CancellationToken cancellationToken)
    {
        await _commitResolved(commit, cancellationToken);
        await _inner.OnCommitAsync(commit, cancellationToken);
    }

    public Task InfoAsync(string message, CancellationToken cancellationToken) =>
        OnOutputAsync(DeploymentConsole.Info(message), cancellationToken);
}
