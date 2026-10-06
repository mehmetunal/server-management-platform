using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Users;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Extensions;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.UserManage)]
public class UsersController : Controller
{
    private readonly IUserManagementService _userManagementService;
    private readonly IRoleManagementService _roleManagementService;

    public UsersController(IUserManagementService userManagementService, IRoleManagementService roleManagementService)
    {
        _userManagementService = userManagementService;
        _roleManagementService = roleManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await _userManagementService.GetUsersAsync(cancellationToken);
        return Request.IsAjax() ? PartialView("_UserList", users) : View(users);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var dto = new CreateUserDto { Roles = [Roles.Viewer] };
        await SetRoleOptionsAsync(dto.Roles, cancellationToken);
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _userManagementService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kullanıcı oluşturulamadı.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userManagementService.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        await SetRoleOptionsAsync(result.Data!.Roles, cancellationToken);
        return View(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, UpdateUserDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _userManagementService.UpdateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kullanıcı güncellenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLock(Guid id, bool locked, CancellationToken cancellationToken)
    {
        var result = await _userManagementService.SetLockAsync(id, locked, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message)
            : this.ApiFailure(result, "Kullanıcının kilit durumu değiştirilemedi.");
    }

    private async Task SetRoleOptionsAsync(IReadOnlyCollection<string> selected, CancellationToken cancellationToken)
    {
        var options = await _roleManagementService.GetRoleOptionsAsync(cancellationToken);
        ViewData[nameof(RoleSelectModel)] = new RoleSelectModel(options, selected, User.IsInRole(Roles.SuperAdmin));
    }

    [HttpPost]
    public async Task<IActionResult> ResetTwoFactor(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userManagementService.ResetTwoFactorAsync(id, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message)
            : this.ApiFailure(result, "İki adımlı doğrulama sıfırlanamadı.");
    }
}
