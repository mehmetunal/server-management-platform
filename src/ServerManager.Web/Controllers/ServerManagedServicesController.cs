using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

/// <summary>Sunucu sayfasındaki Servisler sekmesi: o sunucuya kurulan veritabanı ve uygulama servisleri.</summary>
[HasPermission(Permissions.ServicesView)]
public class ServerManagedServicesController : Controller
{
    private readonly IManagedServiceService _services;
    private readonly ServerPageBuilder _pageBuilder;

    public ServerManagedServicesController(IManagedServiceService services, ServerPageBuilder pageBuilder)
    {
        _services = services;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.ManagedServicesTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        return View(new ManagedServiceIndexViewModel
        {
            Page = page,
            ServerId = id,
            Services = await _services.ListAsync(id, cancellationToken)
        });
    }
}
