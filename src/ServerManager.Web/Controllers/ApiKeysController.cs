using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ServerManager.Application.ApiKeys;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.ApiKeys;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

/// <summary>Hesabım → kişisel API anahtarları ve REST API belgesi. Tüm anahtarların listesi kullanıcı yönetimi izni ister.</summary>
public class ApiKeysController : Controller
{
    private readonly IApiKeyService _apiKeys;
    private readonly IOptionsMonitor<ApiKeyOptions> _options;

    public ApiKeysController(IApiKeyService apiKeys, IOptionsMonitor<ApiKeyOptions> options)
    {
        _apiKeys = apiKeys;
        _options = options;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        return View(new ApiKeysPageViewModel
        {
            Keys = await _apiKeys.GetMineAsync(cancellationToken),
            GrantableScopes = await _apiKeys.GetGrantableScopesAsync(cancellationToken),
            LifetimeOptions = ApiKeyRules.LifetimeOptions(options),
            Enabled = options.Enabled,
            CanManageAll = User.HasPermission(Permissions.UserManage),
            BaseUrl = BaseUrl()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateApiKeyDto dto, CancellationToken cancellationToken)
    {
        var result = await _apiKeys.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "API anahtarı oluşturulamadı.");

        return Ok(ApiResponse<ApiKeyCreatedDto>.Success(result.Data, result.Message ?? "API anahtarı oluşturuldu."));
    }

    [HttpPost]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var result = await _apiKeys.RevokeAsync(id, asAdmin: false, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "API anahtarı iptal edilemedi.");
    }

    [HttpGet]
    [HasPermission(Permissions.UserManage)]
    public async Task<IActionResult> All(CancellationToken cancellationToken) =>
        View(new ApiKeysPageViewModel
        {
            Keys = await _apiKeys.GetAllAsync(cancellationToken),
            Enabled = _options.CurrentValue.Enabled,
            CanManageAll = true,
            BaseUrl = BaseUrl()
        });

    [HttpPost]
    [HasPermission(Permissions.UserManage)]
    public async Task<IActionResult> RevokeAny(Guid id, CancellationToken cancellationToken)
    {
        var result = await _apiKeys.RevokeAsync(id, asAdmin: true, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "API anahtarı iptal edilemedi.");
    }

    [HttpGet]
    public IActionResult Docs() => View(new ApiDocsViewModel
    {
        BaseUrl = BaseUrl(),
        Enabled = _options.CurrentValue.Enabled,
        RequestsPerMinute = _options.CurrentValue.RequestsPerMinute
    });

    private string BaseUrl() => $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/v1";
}
