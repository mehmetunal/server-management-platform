namespace ServerManager.Application.Interfaces.Ssh;

public interface ITerminalOutputSink
{
    Task OnOutputAsync(string data);

    Task OnClosedAsync(string? reason);
}
