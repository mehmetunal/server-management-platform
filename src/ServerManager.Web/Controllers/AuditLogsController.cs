using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Extensions;
using ServerManager.Web.Models;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.AuditView)]
public class AuditLogsController : Controller
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] AuditLogFilterDto filter,
        [FromQuery(Name = AuditLogIndexViewModel.ActionQueryKey)] string? auditAction,
        CancellationToken cancellationToken)
    {
        filter.Action = auditAction;
        var logs = await _auditLogService.SearchAsync(filter, cancellationToken);
        var model = new AuditLogIndexViewModel { Logs = logs, Filter = filter };
        return Request.IsAjax() ? PartialView("_AuditLogList", model) : View(model);
    }
}
