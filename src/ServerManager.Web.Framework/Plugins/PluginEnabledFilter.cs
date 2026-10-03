using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using ServerManager.Application.Plugins;

namespace ServerManager.Web.Framework.Plugins;

/// <summary>Kurulu olmayan veya devre dışı bırakılan eklentinin controller'ları 404 döner.</summary>
public sealed class PluginEnabledFilter : IResourceFilter
{
    private readonly IPluginCatalog _catalog;

    public PluginEnabledFilter(IPluginCatalog catalog)
    {
        _catalog = catalog;
    }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor action
            && !_catalog.IsAssemblyEnabled(action.ControllerTypeInfo.Assembly))
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
