using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Roles;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Extensions;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

/// <summary>Roller ve izinleri. SuperAdmin değişmez; diğer yerleşik roller düzenlenip varsayılana döndürülebilir.</summary>
[HasPermission(Permissions.RolesManage)]
public class RolesController : Controller
{
    private readonly IRoleManagementService _roles;

    public RolesController(IRoleManagementService roles)
    {
        _roles = roles;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var roles = await _roles.GetRolesAsync(cancellationToken);
        return Request.IsAjax() ? PartialView("_RoleList", roles) : View(roles);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? cloneFrom, CancellationToken cancellationToken)
    {
        var result = await _roles.GetForCreateAsync(cloneFrom, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        return View("Form", new RoleFormViewModel { Editor = result.Data! });
    }

    [HttpPost]
    public async Task<IActionResult> Create(RoleFormDto dto, CancellationToken cancellationToken)
    {
        dto.Id = null;
        var result = await _roles.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Rol oluşturulamadı.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _roles.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        ViewData["RoleOptions"] = (await _roles.GetRolesAsync(cancellationToken)).Where(r => r.Id != id).ToList();
        return View("Form", new RoleFormViewModel { Editor = result.Data! });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, RoleFormDto dto, CancellationToken cancellationToken)
    {
        dto.Id = id;
        var result = await _roles.UpdateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Rol güncellenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpPost]
    public async Task<IActionResult> ResetToDefault(Guid id, bool confirmSelfLockout, CancellationToken cancellationToken)
    {
        var result = await _roles.ResetToDefaultAsync(id, confirmSelfLockout, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Rol varsayılana döndürülemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Edit), new { id }));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, Guid? reassignToRoleId, CancellationToken cancellationToken)
    {
        var result = await _roles.DeleteAsync(id, reassignToRoleId, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Rol silinemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }
}
