namespace ServerManager.Web.BackgroundJobs;

/// <summary>
/// Üst sınırı her girişte yeniden okunan eşzamanlılık kapısı. Panelden değiştirilen sınır yeniden başlatma gerektirmez:
/// artırılınca sıradakiler bir sonraki giriş/çıkışta başlar, azaltılınca süren işler kesilmez, yeni girişler sınır altına
/// inene kadar bekler.
/// </summary>
public sealed class DynamicConcurrencyLimiter
{
    private readonly Func<int> _limit;
    private readonly object _lock = new();
    private readonly LinkedList<TaskCompletionSource> _waiters = new();
    private int _active;

    public DynamicConcurrencyLimiter(Func<int> limit)
    {
        _limit = limit;
    }

    public int ActiveCount
    {
        get
        {
            lock (_lock)
                return _active;
        }
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TaskCompletionSource waiter;
        LinkedListNode<TaskCompletionSource> node;
        lock (_lock)
        {
            Pump();
            if (_waiters.Count == 0 && _active < Limit)
            {
                _active++;
                return;
            }

            waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            node = _waiters.AddLast(waiter);
        }

        await using (cancellationToken.Register(() =>
        {
            lock (_lock)
            {
                // Sıra zaten verildiyse (düğüm listeden çıktı) iptal yok sayılır; çağıran Release ile bırakır.
                if (node.List is null)
                    return;

                _waiters.Remove(node);
            }

            waiter.TrySetCanceled(cancellationToken);
        }))
        {
            await waiter.Task;
        }
    }

    public void Release()
    {
        lock (_lock)
        {
            if (_active > 0)
                _active--;
            Pump();
        }
    }

    private int Limit => Math.Max(1, _limit());

    private void Pump()
    {
        var limit = Limit;
        while (_waiters.Count > 0 && _active < limit)
        {
            var next = _waiters.First!;
            _waiters.RemoveFirst();
            _active++;
            next.Value.TrySetResult();
        }
    }
}
