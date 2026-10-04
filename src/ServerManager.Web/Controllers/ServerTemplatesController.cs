using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Templates;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.CommandView)]
public class ServerTemplatesController : Controller
{
    private readonly IServerTemplateService _templateService;

    public ServerTemplatesController(IServerTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var templates = await _templateService.GetTemplatesAsync(cancellationToken);
        return Request.IsAjax() ? PartialView("_TemplateList", templates) : View(templates);
    }

    [HttpGet]
    [HasPermission(Permissions.TemplateManage)]
    public IActionResult Create() => View(new ServerTemplateFormDto());

    [HttpPost]
    [HasPermission(Permissions.TemplateManage)]
    public async Task<IActionResult> Create(ServerTemplateFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _templateService.CreateAsync(dto, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Index)))
            : this.ApiFailure(result, "Şablon oluşturulamadı.");
    }

    [HttpGet]
    [HasPermission(Permissions.TemplateManage)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _templateService.GetForEditAsync(id, cancellationToken);
        return result.IsSuccess ? View(result.Data) : NotFound();
    }

    [HttpPost]
    [HasPermission(Permissions.TemplateManage)]
    public async Task<IActionResult> Edit(Guid id, ServerTemplateFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _templateService.UpdateAsync(dto, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Index)))
            : this.ApiFailure(result, "Şablon güncellenemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.TemplateManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _templateService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Şablon silinemedi.");
    }
}
