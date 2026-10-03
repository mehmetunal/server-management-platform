using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Authorization;
using ServerManager.Web.Models;
using ServerManager.Web.Services;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.TerminalView)]
public class TerminalController : Controller
{
    private readonly ITerminalService _terminalService;
    private readonly ServerPageBuilder _pageBuilder;

    public TerminalController(ITerminalService terminalService, ServerPageBuilder pageBuilder)
    {
        _terminalService = terminalService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.TerminalTab, MetricRange.OneHour, cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpGet]
    public async Task<IActionResult> Sessions(Guid id, int page = 1, CancellationToken cancellationToken = default)
    {
        var sessions = await _terminalService.GetSessionsAsync(id, VisibleUserId(), page, cancellationToken);
        return PartialView("_SessionList", new TerminalSessionListViewModel
        {
            ServerId = id,
            Sessions = sessions,
            ShowsAllUsers = VisibleUserId() is null
        });
    }

    [HttpGet]
    public async Task<IActionResult> Session(Guid id, Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _terminalService.GetSessionAsync(id, sessionId, VisibleUserId(), cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        return PartialView("_SessionCommands", result.Data!);
    }

    [HttpGet]
    [HasPermission(Permissions.TerminalExecute)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Forbid();

        var commands = await _terminalService.GetRecentCommandsAsync(id, userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.Success(commands));
    }

    private string? VisibleUserId() =>
        User.HasPermission(Permissions.AuditView) ? null : User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
