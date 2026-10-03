using NSubstitute;
using ServerManager.Application.Deployments;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Tests.Fakes;

namespace ServerManager.Application.Tests.Deployments;

public class DeploymentRecorderTests
{
    private readonly IDeploymentObserver _inner = Substitute.For<IDeploymentObserver>();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly List<string> _flushes = [];
    private readonly List<DeploymentStage> _stages = [];
    private readonly List<DeploymentCommit> _commits = [];
    private readonly DeploymentRecorder _recorder;

    public DeploymentRecorderTests()
    {
        _recorder = new DeploymentRecorder(
            _inner,
            4096,
            (log, _) =>
            {
                _flushes.Add(log);
                return Task.CompletedTask;
            },
            (stage, _) =>
            {
                _stages.Add(stage);
                return Task.CompletedTask;
            },
            (commit, _) =>
            {
                _commits.Add(commit);
                return Task.CompletedTask;
            },
            _time);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Output_is_forwarded_and_flushed_periodically()
    {
        await _recorder.OnOutputAsync("a", Ct);
        Assert.Empty(_flushes);

        _time.UtcNow = _time.UtcNow.AddSeconds(11);
        await _recorder.OnOutputAsync("b", Ct);

        Assert.Equal(["ab"], _flushes);
        await _inner.Received(1).OnOutputAsync("a", Arg.Any<CancellationToken>());
        await _inner.Received(1).OnOutputAsync("b", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stage_change_is_persisted_logged_and_forwarded()
    {
        await _recorder.OnStageAsync(DeploymentStage.Building, "Build başlıyor", Ct);

        Assert.Equal(DeploymentStage.Building, _recorder.Stage);
        Assert.Equal([DeploymentStage.Building], _stages);
        Assert.Contains("==> Build başlıyor", _recorder.Log.ToString());
        await _inner.Received(1).OnStageAsync(DeploymentStage.Building, "Build başlıyor", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolved_commit_is_persisted_before_it_is_forwarded()
    {
        var commit = new DeploymentCommit(new string('a', 40), "Ayşe", "İlk sürüm");
        _inner.OnCommitAsync(commit, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Assert.Equal([commit], _commits);
            return Task.CompletedTask;
        });

        await _recorder.OnCommitAsync(commit, Ct);

        await _inner.Received(1).OnCommitAsync(commit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Log_keeps_the_tail_when_limit_is_exceeded()
    {
        var log = new DeploymentLog(1024);
        log.Append(new string('a', 1000));
        log.Append(new string('b', 100));

        var text = log.ToString();
        Assert.StartsWith("[… logun başı kısaltıldı …]", text);
        Assert.EndsWith(new string('b', 100), text);
        Assert.Equal(1024, text.Length - "[… logun başı kısaltıldı …]\r\n".Length);
    }

    [Fact]
    public void Cancellation_distinguishes_user_cancel_from_shutdown()
    {
        using var shutdown = new CancellationTokenSource();
        using var cancellation = new DeploymentCancellation(shutdown.Token);
        var actor = new DTOs.Deployments.DeploymentActor(null, "admin", "127.0.0.1");

        Assert.True(cancellation.Cancel(actor));
        Assert.False(cancellation.Cancel(actor));
        Assert.True(cancellation.Token.IsCancellationRequested);
        Assert.True(cancellation.IsCancelledByUser);
        Assert.False(cancellation.IsShutdown);
        Assert.Equal("admin", cancellation.CancelledBy?.UserName);

        using var other = new DeploymentCancellation(shutdown.Token);
        shutdown.Cancel();
        Assert.True(other.Token.IsCancellationRequested);
        Assert.True(other.IsShutdown);
        Assert.False(other.IsCancelledByUser);
    }
}
