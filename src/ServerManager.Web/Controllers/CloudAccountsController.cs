using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.CloudView)]
public class CloudAccountsController : Controller
{
    private readonly ICloudAccountService _cloudService;
    private readonly IServerTemplateService _templateService;

    public CloudAccountsController(ICloudAccountService cloudService, IServerTemplateService templateService)
    {
        _cloudService = cloudService;
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var accounts = await _cloudService.GetAccountsAsync(cancellationToken);
        ViewData["HasProviders"] = _cloudService.GetProviders().Count > 0;
        return Request.IsAjax() ? PartialView("_AccountList", accounts) : View(accounts);
    }

    [HttpGet]
    public async Task<IActionResult> Servers(Guid id, CancellationToken cancellationToken)
    {
        var result = await _cloudService.GetServersAsync(id, cancellationToken);
        if (result.ErrorType == Application.Common.ServiceErrorType.NotFound)
            return NotFound();

        ViewData["AccountId"] = id;
        ViewData["Error"] = result.IsSuccess ? null : result.Message;
        return Request.IsAjax() ? PartialView("_ServerList", result.Data) : View(result.Data);
    }

    [HttpGet]
    [HasPermission(Permissions.CloudManage)]
    public IActionResult Create() => View(FormModel(new CloudAccountFormDto()));

    [HttpPost]
    [HasPermission(Permissions.CloudManage)]
    public async Task<IActionResult> Create(CloudAccountFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _cloudService.CreateAsync(dto, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Servers), new { id = result.Data }))
            : this.ApiFailure(result, "Hesap eklenemedi.");
    }

    [HttpGet]
    [HasPermission(Permissions.CloudManage)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _cloudService.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        var providerName = (await _cloudService.GetAccountsAsync(cancellationToken)).FirstOrDefault(a => a.Id == id)?.ProviderName;
        return View(FormModel(result.Data!, providerName));
    }

    [HttpPost]
    [HasPermission(Permissions.CloudManage)]
    public async Task<IActionResult> Edit(Guid id, CloudAccountFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _cloudService.UpdateAsync(dto, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Index)))
            : this.ApiFailure(result, "Hesap güncellenemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.CloudManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _cloudService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Hesap silinemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.CloudManage)]
    public async Task<IActionResult> Sync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _cloudService.SyncAsync(id, cancellationToken: cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Eşitleme başarısız.");
    }

    [HttpGet]
    [HasPermission(Permissions.CloudProvision)]
    public async Task<IActionResult> Provision(Guid? accountId, CancellationToken cancellationToken)
    {
        var accounts = await _cloudService.GetOptionsAsync(cancellationToken);
        return View(new CloudProvisionViewModel
        {
            Form = new CloudProvisionDto { AccountId = accountId is { } id && accounts.Any(a => a.Id == id) ? id : accounts.FirstOrDefault()?.Id ?? Guid.Empty },
            Accounts = accounts,
            Templates = await _templateService.GetOptionsAsync(ServerTemplateKind.CloudInit, cancellationToken)
        });
    }

    [HttpGet]
    [HasPermission(Permissions.CloudProvision)]
    public async Task<IActionResult> Catalog(Guid id, CancellationToken cancellationToken)
    {
        var result = await _cloudService.GetCatalogAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<CloudCatalog>.Success(result.Data!, "Seçenekler yüklendi."))
            : this.ApiFailure(result, "Sağlayıcı seçenekleri alınamadı.");
    }

    [HttpPost]
    [HasPermission(Permissions.CloudProvision)]
    public async Task<IActionResult> Provision(CloudProvisionDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _cloudService.ProvisionAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Sunucu oluşturulamadı.");

        var payload = new CloudProvisionResponse(Url.Action(nameof(Servers), new { id = result.Data!.AccountId })!, result.Data.RootPassword);
        return Ok(ApiResponse<CloudProvisionResponse>.Success(payload, result.Message ?? "Sunucu oluşturuldu."));
    }

    private CloudAccountFormViewModel FormModel(CloudAccountFormDto form, string? providerName = null) => new()
    {
        Form = form,
        Providers = _cloudService.GetProviders(),
        ProviderName = providerName
    };
}
