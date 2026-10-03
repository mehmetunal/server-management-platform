namespace ServerManager.Application.Interfaces.Ssh;

public interface ITerminalSession : IAsyncDisposable
{
    DateTime StartedAt { get; }

    DateTime LastActivityAt { get; }

    bool IsClosed { get; }

    Task WriteAsync(string data, CancellationToken cancellationToken = default);

    void Resize(int columns, int rows);
}
