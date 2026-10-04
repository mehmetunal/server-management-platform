using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Commands;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Commands;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.CommandView)]
public class CommandRunsController : Controller
{
    private readonly ICommandRunService _runService;
    private readonly IServerService _serverService;
    private readonly IServerGroupService _groupService;
    private readonly IServerTemplateService _templateService;
    private readonly CommandRunManager _manager;

    public CommandRunsController(
        ICommandRunService runService,
        IServerService serverService,
        IServerGroupService groupService,
        IServerTemplateService templateService,
        CommandRunManager manager)
    {
        _runService = runService;
        _serverService = serverService;
        _groupService = groupService;
        _templateService = templateService;
        _manager = manager;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CommandRunFilterDto filter, CancellationToken cancellationToken)
    {
        var model = new CommandRunIndexViewModel
        {
            Runs = await _runService.SearchAsync(filter, cancellationToken),
            Filter = filter
        };
        return Request.IsAjax() ? PartialView("_RunList", model) : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _runService.GetAsync(id, cancellationToken);
        return result.IsSuccess ? View(result.Data) : NotFound();
    }

    [HttpGet]
    [HasPermission(Permissions.CommandRun)]
    public async Task<IActionResult> Create(Guid? serverId, Guid? groupId, Guid? templateId, CancellationToken cancellationToken)
    {
        var servers = await _serverService.GetOptionsAsync(cancellationToken);
        var form = new CommandRunRequestDto { TemplateId = templateId };
        if (serverId is { } sid && servers.Any(s => s.Id == sid))
            form.ServerIds.Add(sid);
        if (groupId is { } gid)
            form.ServerIds.AddRange(servers.Where(s => s.GroupId == gid && !form.ServerIds.Contains(s.Id)).Select(s => s.Id));

        var templates = await _templateService.GetOptionsAsync(ServerTemplateKind.Script, cancellationToken);
        if (templates.FirstOrDefault(t => t.Id == templateId) is { } template)
        {
            form.Command = template.Content;
            form.UseSudo = template.RequiresSudo;
        }

        return View(new CommandRunFormViewModel
        {
            Form = form,
            Servers = servers,
            Groups = await _groupService.GetOptionsAsync(cancellationToken),
            Templates = templates
        });
    }

    [HttpPost]
    [HasPermission(Permissions.CommandRun)]
    public async Task<IActionResult> Create(CommandRunRequestDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _manager.StartAsync(_runService, dto, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Details), new { id = result.Data }))
            : this.ApiFailure(result, "Komut başlatılamadı.");
    }
}
