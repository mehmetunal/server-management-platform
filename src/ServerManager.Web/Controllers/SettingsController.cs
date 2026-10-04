using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Settings;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.SettingsView)]
public class SettingsController : Controller
{
    private readonly ISystemInfoService _systemInfo;
    private readonly IPanelSettingsService _panelSettings;

    public SettingsController(ISystemInfoService systemInfo, IPanelSettingsService panelSettings)
    {
        _systemInfo = systemInfo;
        _panelSettings = panelSettings;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new SettingsPageViewModel
        {
            Info = await _systemInfo.GetAsync(cancellationToken),
            Groups = _panelSettings.GetGroups()
        });

    [HttpPost]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> Save(CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in PanelSettingCatalog.All)
        {
            if (Request.Form.TryGetValue(definition.Key, out var value))
                values[definition.Key] = value.ToString();
        }

        var result = await _panelSettings.SaveAsync(values, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message)
            : this.ApiFailure(result, "Ayarlar kaydedilemedi.");
    }
}
