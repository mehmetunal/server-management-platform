using ServerManager.Application.DTOs.Backups;

namespace ServerManager.Application.Backups;

/// <summary>Kullanıcı iptali "İptal edildi", uygulamanın kapanması "Kesildi" olarak kaydedilir.</summary>
public sealed class BackupCancellation : IDisposable
{
    private readonly CancellationTokenSource _user = new();
    private readonly CancellationTokenSource _linked;
    private readonly CancellationToken _shutdown;

    public BackupCancellation(CancellationToken shutdown)
    {
        _shutdown = shutdown;
        _linked = CancellationTokenSource.CreateLinkedTokenSource(_user.Token, shutdown);
    }

    public CancellationToken Token => _linked.Token;

    public bool IsShutdown => _shutdown.IsCancellationRequested;

    public bool IsCancelledByUser => _user.IsCancellationRequested;

    public BackupActor? CancelledBy { get; private set; }

    /// <returns>İptal ilk kez istendiyse true.</returns>
    public bool Cancel(BackupActor actor)
    {
        lock (_user)
        {
            if (_user.IsCancellationRequested)
                return false;

            CancelledBy = actor;
            _user.Cancel();
            return true;
        }
    }

    public void Dispose()
    {
        _linked.Dispose();
        _user.Dispose();
    }
}
