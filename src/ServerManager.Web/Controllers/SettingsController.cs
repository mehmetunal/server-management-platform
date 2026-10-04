using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.SettingsView)]
public class SettingsController : Controller
{
    private readonly ISystemInfoService _systemInfo;

    public SettingsController(ISystemInfoService systemInfo)
    {
        _systemInfo = systemInfo;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _systemInfo.GetAsync(cancellationToken));
}
