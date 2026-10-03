using NSubstitute;
using ServerManager.Plugin.DevOps.Dokploy.Core;
using ServerManager.Plugin.DevOps.Dokploy.Services;
using ServerManager.Plugin.DevOps.Dokploy.Tests.Fakes;

namespace ServerManager.Plugin.DevOps.Dokploy.Tests.Core;

public class DokployInstallRecorderTests
{
    private readonly IDokployInstallObserver _inner = Substitute.For<IDokployInstallObserver>();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly List<string> _flushes = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DokployInstallRecorder CreateRecorder(int maxChars = 4096) =>
        new(_inner, maxChars, (output, _) =>
        {
            _flushes.Add(output);
            return Task.CompletedTask;
        }, _time);

    [Fact]
    public async Task Forwards_output_and_keeps_log()
    {
        var recorder = CreateRecorder();

        await recorder.OnOutputAsync("line 1\r\n", Ct);
        await recorder.OnStageAsync(DokployInstallStage.Installing, "Kuruluyor", Ct);

        await _inner.Received(1).OnOutputAsync("line 1\r\n", Arg.Any<CancellationToken>());
        await _inner.Received(1).OnStageAsync(DokployInstallStage.Installing, "Kuruluyor", Arg.Any<CancellationToken>());
        Assert.Equal("line 1\r\n", recorder.Log.ToString());
    }

    [Fact]
    public async Task Flushes_periodically()
    {
        var recorder = CreateRecorder();

        await recorder.OnOutputAsync("a", Ct);
        Assert.Empty(_flushes);

        _time.UtcNow = _time.UtcNow.AddSeconds(11);
        await recorder.OnOutputAsync("b", Ct);
        await recorder.OnOutputAsync("c", Ct);

        Assert.Equal(["ab"], _flushes);
    }

    [Fact]
    public async Task Info_lines_are_colored()
    {
        var recorder = CreateRecorder();

        await recorder.InfoAsync("Betik indiriliyor", Ct);

        Assert.Equal("\u001b[36m==> Betik indiriliyor\u001b[0m\r\n", recorder.Log.ToString());
    }

    [Fact]
    public void Log_keeps_tail_and_marks_truncation()
    {
        var log = new DokployInstallLog(1024);

        log.Append(new string('a', 1000));
        log.Append(new string('b', 100));

        var text = log.ToString();
        Assert.StartsWith("[… çıktının başı kısaltıldı …]", text);
        Assert.EndsWith(new string('b', 100), text);
        Assert.Equal(1024, text.Length - "[… çıktının başı kısaltıldı …]\r\n".Length);
    }
}
