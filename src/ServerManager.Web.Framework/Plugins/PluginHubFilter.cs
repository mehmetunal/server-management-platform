using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Plugins;

namespace ServerManager.Web.Framework.Plugins;

/// <summary>Devre dışı eklentinin hub'ına bağlantı ve metot çağrısı reddedilir.</summary>
public sealed class PluginHubFilter : IHubFilter
{
    private const string DisabledMessage = "Bu eklenti devre dışı.";

    private readonly IPluginCatalog _catalog;

    public PluginHubFilter(IPluginCatalog catalog)
    {
        _catalog = catalog;
    }

    public ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        EnsureEnabled(invocationContext.Hub);
        return next(invocationContext);
    }

    public Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        EnsureEnabled(context.Hub);
        return next(context);
    }

    private void EnsureEnabled(Hub hub)
    {
        if (!_catalog.IsAssemblyEnabled(hub.GetType().Assembly))
            throw new HubException(DisabledMessage);
    }
}
