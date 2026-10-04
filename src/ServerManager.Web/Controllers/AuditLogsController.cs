using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Extensions;
using ServerManager.Web.Models;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.AuditView)]
public class AuditLogsController : Controller
{
    private static readonly TimeSpan StatsWindow = TimeSpan.FromHours(24);

    private readonly IAuditLogService _auditLogService;
    private readonly AuditActionCatalog _actionCatalog;

    public AuditLogsController(IAuditLogService auditLogService, AuditActionCatalog actionCatalog)
    {
        _auditLogService = auditLogService;
        _actionCatalog = actionCatalog;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] AuditLogFilterDto filter,
        [FromQuery(Name = AuditLogIndexViewModel.ActionQueryKey)] string? auditAction,
        CancellationToken cancellationToken)
    {
        filter.Action = auditAction;
        var logs = await _auditLogService.SearchAsync(filter, cancellationToken);

        if (Request.IsAjax())
            return PartialView("_AuditLogList", new AuditLogIndexViewModel { Logs = logs, Filter = filter });

        var model = new AuditLogIndexViewModel
        {
            Logs = logs,
            Filter = filter,
            Stats = await _auditLogService.GetStatsAsync(StatsWindow, cancellationToken),
            EntityTypes = await _auditLogService.GetEntityTypesAsync(cancellationToken)
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var log = await _auditLogService.GetAsync(id, cancellationToken);
        return log is null ? NotFound() : View(log);
    }

    [HttpGet]
    [HasPermission(Permissions.AuditExport)]
    [EnableRateLimiting(RateLimitPolicies.AuditAction)]
    public async Task<IActionResult> Export(
        [FromQuery] AuditLogFilterDto filter,
        [FromQuery(Name = AuditLogIndexViewModel.ActionQueryKey)] string? auditAction,
        CancellationToken cancellationToken)
    {
        filter.Action = auditAction;
        var export = await _auditLogService.ExportAsync(filter, cancellationToken);
        var content = AuditCsv.Write(export.Items, _actionCatalog.DisplayName);
        var fileName = $"audit-log-{AppTimeZone.ToLocal(DateTime.UtcNow):yyyyMMdd-HHmm}.csv";
        return File(content, "text/csv; charset=utf-8", fileName);
    }

    [HttpPost]
    [HasPermission(Permissions.AuditExport)]
    [EnableRateLimiting(RateLimitPolicies.AuditAction)]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        var result = await _auditLogService.VerifyChainAsync(cancellationToken);
        return Ok(ApiResponse<AuditChainVerificationDto>.Success(result, result.Message));
    }
}
