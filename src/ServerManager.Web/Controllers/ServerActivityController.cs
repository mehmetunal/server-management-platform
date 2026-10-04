using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

/// <summary>Sunucu sayfasındaki Activity sekmesi: bu sunucu üzerinde yapılan işlemlerin audit kayıtları.</summary>
[HasPermission(Permissions.AuditView)]
public class ServerActivityController : Controller
{
    private readonly IAuditLogService _auditLogService;
    private readonly ServerPageBuilder _pageBuilder;

    public ServerActivityController(IAuditLogService auditLogService, ServerPageBuilder pageBuilder)
    {
        _auditLogService = auditLogService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        Guid id,
        [FromQuery] AuditLogFilterDto filter,
        [FromQuery(Name = AuditLogIndexViewModel.ActionQueryKey)] string? auditAction,
        CancellationToken cancellationToken)
    {
        filter.Action = auditAction;
        filter.EntityType = AuditEntityTypes.Server;
        filter.EntityId = id.ToString();
        var list = new AuditLogIndexViewModel { Logs = await _auditLogService.SearchAsync(filter, cancellationToken), Filter = filter };
        if (Request.IsAjax())
            return PartialView("~/Views/AuditLogs/_AuditLogList.cshtml", list);

        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.ActivityTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        return View(new ServerActivityViewModel { Page = page, List = list });
    }
}
