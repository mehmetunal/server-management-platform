using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.PluginManage)]
public class PluginsController : Controller
{
    private readonly IPluginService _pluginService;

    public PluginsController(IPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var plugins = await _pluginService.GetPluginsAsync(cancellationToken);
        return Request.IsAjax() ? PartialView("_PluginList", plugins) : View(plugins);
    }

    [HttpPost]
    public async Task<IActionResult> Install(string systemName, CancellationToken cancellationToken)
    {
        var result = await _pluginService.InstallAsync(systemName, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message)
            : this.ApiFailure(result, "Eklenti kurulamadı.");
    }

    [HttpPost]
    public async Task<IActionResult> SetEnabled(string systemName, bool enabled, CancellationToken cancellationToken)
    {
        var result = await _pluginService.SetEnabledAsync(systemName, enabled, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message)
            : this.ApiFailure(result, "Eklentinin durumu değiştirilemedi.");
    }
}
