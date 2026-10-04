using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.BackupManage)]
public class BackupStoragesController : Controller
{
    public const string ProviderKey = "BackupStorageProvider";

    private readonly IBackupStorageService _storageService;

    public BackupStoragesController(IBackupStorageService storageService)
    {
        _storageService = storageService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new BackupStorageIndexViewModel
        {
            Storages = await _storageService.GetStoragesAsync(cancellationToken),
            Providers = _storageService.GetProviders()
        };
        return Request.IsAjax() ? PartialView("_StorageList", model) : View(model);
    }

    [HttpGet]
    public IActionResult Create(string? provider)
    {
        var descriptor = _storageService.GetProviders().FirstOrDefault(p => p.SystemName == provider);
        if (descriptor is null)
            return RedirectToAction(nameof(Index));

        ViewData[ProviderKey] = descriptor;
        return View(new BackupStorageFormDto { ProviderSystemName = descriptor.SystemName, Name = descriptor.DisplayName });
    }

    [HttpPost]
    public async Task<IActionResult> Create(BackupStorageFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _storageService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Depolama hedefi eklenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _storageService.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        var descriptor = _storageService.GetProviders().FirstOrDefault(p => p.SystemName == result.Data!.ProviderSystemName);
        if (descriptor is null)
            return RedirectToAction(nameof(Index));

        ViewData[ProviderKey] = descriptor;
        return View(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, BackupStorageFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _storageService.UpdateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Depolama hedefi güncellenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _storageService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Depolama hedefi silinemedi.");

        return this.ApiSuccess(result.Message);
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.BackupAction)]
    public async Task<IActionResult> Test(Guid id, CancellationToken cancellationToken)
    {
        var result = await _storageService.TestAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Depolama testi başarısız.");

        return this.ApiSuccess(result.Message);
    }
}
