using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Users;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Authorization;
using ServerManager.Web.Extensions;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.UserManage)]
public class UsersController : Controller
{
    private readonly IUserManagementService _userManagementService;

    public UsersController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await _userManagementService.GetUsersAsync(cancellationToken);
        return View(users);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateUserDto());

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserDto dto, CancellationToken cancellationToken)
    {
        var result = await _userManagementService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddServiceErrors(result);
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userManagementService.GetForEditAsync(id, cancellationToken);
        return result.IsSuccess ? View(result.Data) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, UpdateUserDto dto, CancellationToken cancellationToken)
    {
        dto.Id = id;
        var result = await _userManagementService.UpdateAsync(dto, cancellationToken);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound();

        if (!result.IsSuccess)
        {
            ModelState.AddServiceErrors(result);
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLock(Guid id, bool locked, CancellationToken cancellationToken)
    {
        var result = await _userManagementService.SetLockAsync(id, locked, cancellationToken);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound();

        TempData[result.IsSuccess ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
