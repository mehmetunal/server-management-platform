using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Web.Hubs;

namespace ServerManager.Web.Services;

public sealed class SignalRTerminalOutputSink : ITerminalOutputSink
{
    private readonly ISingleClientProxy _client;
    private readonly Func<string?, Task> _onClosed;

    public SignalRTerminalOutputSink(ISingleClientProxy client, Func<string?, Task> onClosed)
    {
        _client = client;
        _onClosed = onClosed;
    }

    public Task OnOutputAsync(string data) =>
        _client.SendAsync(ContainerTerminalHub.OutputEvent, data);

    public async Task OnClosedAsync(string? reason)
    {
        try
        {
            await _client.SendAsync(ContainerTerminalHub.ClosedEvent, reason);
        }
        finally
        {
            await _onClosed(reason);
        }
    }
}
