using System.Text;
using ServerManager.Application.Deployments;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Interfaces.ManagedServices;
using ServerManager.Application.ManagedServices;

namespace ServerManager.E2E.Tests.Infrastructure;

/// <summary>Arka plan işlemlerinin (servis kurulumu, deployment) canlı çıktısını biriktirir; hata mesajında gösterilir.</summary>
public sealed class RecordingObserver : IServiceOperationObserver, IDeploymentObserver
{
    private readonly StringBuilder _log = new();
    private readonly Lock _lock = new();

    public string Log
    {
        get
        {
            lock (_lock)
                return _log.ToString();
        }
    }

    public Task OnOutputAsync(string text, CancellationToken cancellationToken)
    {
        lock (_lock)
            _log.Append(text);
        return Task.CompletedTask;
    }

    public Task OnStageAsync(ServiceOperationStage stage, string message, CancellationToken cancellationToken) =>
        OnOutputAsync($"\n[stage {stage}] {message}\n", cancellationToken);

    public Task OnStageAsync(DeploymentStage stage, string message, CancellationToken cancellationToken) =>
        OnOutputAsync($"\n[stage {stage}] {message}\n", cancellationToken);

    public Task OnCommitAsync(DeploymentCommit commit, CancellationToken cancellationToken) =>
        OnOutputAsync($"\n[commit] {commit}\n", cancellationToken);

    /// <summary>Hata mesajında logun yalnızca son kısmı.</summary>
    public string Tail(int characters = 6000)
    {
        var log = Log;
        return log.Length <= characters ? log : "…" + log[^characters..];
    }
}

public static class Waiter
{
    /// <summary>Koşul sağlanana kadar yoklar; süre dolarsa son durumu mesajla birlikte testi düşürür.</summary>
    public static async Task<T> UntilAsync<T>(Func<Task<T>> probe, Func<T, bool> done, TimeSpan timeout, string what, TimeSpan? interval = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        T last;
        while (true)
        {
            last = await probe();
            if (done(last))
                return last;
            if (DateTime.UtcNow > deadline)
                break;
            await Task.Delay(interval ?? TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }

        Assert.Fail($"{what}: {timeout.TotalSeconds:0} sn içinde gerçekleşmedi. Son durum: {last}");
        return last;
    }
}
