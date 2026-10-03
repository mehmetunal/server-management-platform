using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Authorization;
using ServerManager.Web.Extensions;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.ServerView)]
public class ServersController : Controller
{
    private static readonly string[] SecretFieldNames =
    [
        nameof(ServerFormDto.Password),
        nameof(ServerFormDto.PrivateKey),
        nameof(ServerFormDto.Passphrase),
        nameof(ServerFormDto.SudoPassword)
    ];

    private readonly IServerService _serverService;

    public ServersController(IServerService serverService)
    {
        _serverService = serverService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken)
    {
        var servers = await _serverService.SearchAsync(filter, cancellationToken);
        var tags = await _serverService.GetTagNamesAsync(cancellationToken);

        return View(new ServerIndexViewModel
        {
            Servers = servers,
            Filter = filter,
            Tags = tags
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serverService.GetDetailsAsync(id, cancellationToken);
        return result.IsSuccess ? View(result.Data) : NotFound();
    }

    [HttpGet]
    [HasPermission(Permissions.ServerCreate)]
    public IActionResult Create() => View(new CreateServerDto());

    [HttpPost]
    [HasPermission(Permissions.ServerCreate)]
    public async Task<IActionResult> Create(CreateServerDto dto, CancellationToken cancellationToken)
    {
        var result = await _serverService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddServiceErrors(result);
            ClearSecrets(dto);
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }

    [HttpGet]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serverService.GetForEditAsync(id, cancellationToken);
        return result.IsSuccess ? View(result.Data) : NotFound();
    }

    [HttpPost]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> Edit(Guid id, UpdateServerDto dto, CancellationToken cancellationToken)
    {
        dto.Id = id;
        var result = await _serverService.UpdateAsync(dto, cancellationToken);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound();

        if (!result.IsSuccess)
        {
            ModelState.AddServiceErrors(result);
            ClearSecrets(dto);
            await RestoreSecretFlagsAsync(dto, cancellationToken);
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [HasPermission(Permissions.ServerDelete)]
    public async Task<IActionResult> Delete(Guid id, string? confirmationName, CancellationToken cancellationToken)
    {
        var result = await _serverService.DeleteAsync(id, confirmationName, cancellationToken);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound();

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Errors.FirstOrDefault()?.Message ?? result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [HasPermission(Permissions.ServerConnect)]
    [EnableRateLimiting(RateLimitPolicies.ConnectionTest)]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serverService.TestConnectionAsync(id, cancellationToken);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound(ApiResponse<ConnectionTestResultDto>.Fail(result.Message ?? "Sunucu bulunamadı.", StatusCodes.Status404NotFound));

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<ConnectionTestResultDto>.Fail(result.Message ?? "Bağlantı testi yapılamadı.", StatusCodes.Status400BadRequest));

        return Ok(ApiResponse<ConnectionTestResultDto>.Success(result.Data, result.Data!.Message));
    }

    private void ClearSecrets(ServerFormDto dto)
    {
        dto.ClearSecrets();
        foreach (var name in SecretFieldNames)
        {
            if (ModelState.TryGetValue(name, out var entry))
            {
                entry.RawValue = null;
                entry.AttemptedValue = null;
            }
        }
    }

    private async Task RestoreSecretFlagsAsync(UpdateServerDto dto, CancellationToken cancellationToken)
    {
        var current = await _serverService.GetForEditAsync(dto.Id, cancellationToken);
        if (current.Data is null)
            return;

        dto.HasPassword = current.Data.HasPassword;
        dto.HasPrivateKey = current.Data.HasPrivateKey;
        dto.HasPassphrase = current.Data.HasPassphrase;
        dto.HasSudoPassword = current.Data.HasSudoPassword;
    }
}
