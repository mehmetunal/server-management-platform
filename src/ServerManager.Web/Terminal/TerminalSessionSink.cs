using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Web.Terminal;

public sealed class TerminalSessionSink : ITerminalOutputSink
{
    private readonly Func<string, Task> _onOutput;
    private readonly Func<string?, Task> _onClosed;

    public TerminalSessionSink(Func<string, Task> onOutput, Func<string?, Task> onClosed)
    {
        _onOutput = onOutput;
        _onClosed = onClosed;
    }

    public Task OnOutputAsync(string data) => _onOutput(data);

    public Task OnClosedAsync(string? reason) => _onClosed(reason);
}
