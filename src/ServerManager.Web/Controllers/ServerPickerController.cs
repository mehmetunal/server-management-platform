using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Helpers;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

/// <summary>Menüdeki sunucuya bağlı bölümler için sunucu seçme sayfası; seçilen sunucunun ilgili sekmesine gider.</summary>
[HasPermission(Permissions.ServerView)]
public class ServerPickerController : Controller
{
    private readonly IServerService _serverService;
    private readonly IMonitoringService _monitoringService;
    private readonly MonitoringOptions _monitoringOptions;

    public ServerPickerController(IServerService serverService, IMonitoringService monitoringService, IOptions<MonitoringOptions> monitoringOptions)
    {
        _serverService = serverService;
        _monitoringService = monitoringService;
        _monitoringOptions = monitoringOptions.Value;
    }

    [HttpGet, HasPermission(Permissions.DockerView)]
    public Task<IActionResult> Docker([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Docker, filter, cancellationToken);

    [HttpGet, HasPermission(Permissions.DockerView)]
    public Task<IActionResult> Images([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Images, filter, cancellationToken);

    [HttpGet, HasPermission(Permissions.DockerView)]
    public Task<IActionResult> Volumes([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Volumes, filter, cancellationToken);

    [HttpGet, HasPermission(Permissions.DockerView)]
    public Task<IActionResult> Networks([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Networks, filter, cancellationToken);

    [HttpGet, HasPermission(Permissions.TerminalView)]
    public Task<IActionResult> Terminal([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Terminal, filter, cancellationToken);

    [HttpGet, HasPermission(Permissions.FileView)]
    public Task<IActionResult> Files([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Files, filter, cancellationToken);

    [HttpGet, HasPermission(Permissions.SystemView)]
    public Task<IActionResult> Services([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Services, filter, cancellationToken);

    [HttpGet, HasPermission(Permissions.SystemView)]
    public Task<IActionResult> Processes([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Processes, filter, cancellationToken);

    [HttpGet, HasPermission(Permissions.SystemView)]
    public Task<IActionResult> Logs([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Logs, filter, cancellationToken);

    [HttpGet]
    public Task<IActionResult> Metrics([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken) =>
        PickAsync(ServerPickerSections.Metrics, filter, cancellationToken);

    private async Task<IActionResult> PickAsync(ServerPickerSection section, ServerFilterDto filter, CancellationToken cancellationToken)
    {
        var servers = await _serverService.SearchAsync(filter, cancellationToken);
        var resources = await _monitoringService.GetLatestSummariesAsync(servers.Items.Select(s => s.Id).ToList(), cancellationToken);
        var model = new ServerPickerViewModel
        {
            Section = section,
            Servers = servers,
            Filter = filter,
            Resources = resources,
            Thresholds = _monitoringOptions
        };

        return Request.IsAjax() ? PartialView("_PickerList", model) : View("Index", model);
    }
}
