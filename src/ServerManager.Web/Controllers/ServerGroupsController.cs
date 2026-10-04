using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.ServerGroups;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.ServerView)]
public class ServerGroupsController : Controller
{
    private readonly IServerGroupService _groupService;
    private readonly IServerService _serverService;

    public ServerGroupsController(IServerGroupService groupService, IServerService serverService)
    {
        _groupService = groupService;
        _serverService = serverService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var groups = await _groupService.GetGroupsAsync(cancellationToken);
        return Request.IsAjax() ? PartialView("_GroupList", groups) : View(groups);
    }

    [HttpGet]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View(await FormAsync(new ServerGroupFormDto(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> Create(ServerGroupFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _groupService.CreateAsync(dto, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Index)))
            : this.ApiFailure(result, "Grup oluşturulamadı.");
    }

    [HttpGet]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _groupService.GetForEditAsync(id, cancellationToken);
        return result.IsSuccess ? View(await FormAsync(result.Data!, cancellationToken)) : NotFound();
    }

    [HttpPost]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> Edit(Guid id, ServerGroupFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _groupService.UpdateAsync(dto, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Index)))
            : this.ApiFailure(result, "Grup güncellenemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _groupService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Grup silinemedi.");
    }

    private async Task<ServerGroupFormViewModel> FormAsync(ServerGroupFormDto form, CancellationToken cancellationToken) => new()
    {
        Form = form,
        Servers = await _serverService.GetOptionsAsync(cancellationToken)
    };
}
