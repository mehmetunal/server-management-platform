using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.ServerView)]
public class CostsController : Controller
{
    private readonly ICostReportService _reportService;

    public CostsController(ICostReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _reportService.GetReportAsync(cancellationToken));
}
