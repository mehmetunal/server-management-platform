using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Authorization;
using ServerManager.Web.Models;

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
        return View(new AuditLogIndexViewModel { Logs = logs, Filter = filter });
    }
}
