using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.DTOs;
using ServerManager.Plugin.Git.GitHub.Models;
using ServerManager.Plugin.Git.GitHub.Services;
using ServerManager.Plugin.Git.GitHub.Validators;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Framework.Security;

namespace ServerManager.Plugin.Git.GitHub.Controllers;

[HasPermission(GitHubPermissions.Manage)]
public class GitHubController : Controller
{
    private const string InstalledResult = "installed";

    private readonly IGitHubAppService _appService;
    private readonly GitHubOptions _options;

    public GitHubController(IGitHubAppService appService, IOptions<GitHubOptions> options)
    {
        _appService = appService;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? result, CancellationToken cancellationToken) =>
        await IndexViewAsync(
            result == InstalledResult ? "GitHub kurulumu kaydedildi. Proje formunda bu hesabın depolarını seçebilirsiniz." : null,
            isError: false,
            cancellationToken);

    [HttpGet]
    public async Task<IActionResult> AppList(CancellationToken cancellationToken) =>
        PartialView("_AppList", await _appService.GetAppsAsync(cancellationToken));

    [HttpPost]
    [EnableRateLimiting(GitHubPlugin.RateLimitPolicy)]
    public async Task<IActionResult> Manifest(GitHubManifestRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _appService.StartManifestAsync(dto, PanelBaseUrl, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<GitHubManifestStartDto>.Success(result.Data, "GitHub'a yönlendiriliyorsunuz…"))
            : this.ApiFailure(result, "GitHub App oluşturma başlatılamadı.");
    }

    /// <summary>
    /// GitHub manifest onayından sonra <c>?code=&amp;state=</c> ile buraya döner. Oturum çerezi SameSite=Strict olduğu için
    /// siteler arası dönüşte gönderilmez; sayfa aynı siteden <see cref="CompleteManifest"/> adresine geçer.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Callback(string? code, string? state) =>
        View("Return", new GitHubReturnViewModel
        {
            Title = "GitHub App oluşturuldu",
            Message = "Uygulama bilgileri panele kaydediliyor…",
            TargetUrl = Url.Action(nameof(CompleteManifest), new { code, state })!
        });

    [HttpGet]
    [EnableRateLimiting(GitHubPlugin.RateLimitPolicy)]
    public async Task<IActionResult> CompleteManifest(string? code, string? state, CancellationToken cancellationToken)
    {
        var result = await _appService.CompleteManifestAsync(code, state, cancellationToken);
        if (result.IsSuccess)
            return Redirect(result.Data!);

        return await IndexViewAsync(
            result.Errors.FirstOrDefault()?.Message ?? result.Message ?? "GitHub App kaydedilemedi.",
            isError: true,
            cancellationToken);
    }

    /// <summary>GitHub'da uygulama kurulduğunda veya kurulum değiştirildiğinde buraya döner (bkz. <see cref="Callback"/>).</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Setup() =>
        View("Return", new GitHubReturnViewModel
        {
            Title = "GitHub kurulumu tamamlandı",
            Message = "Panele dönülüyor…",
            TargetUrl = Url.Action(nameof(Installed))!
        });

    [HttpGet]
    public async Task<IActionResult> Installed(CancellationToken cancellationToken)
    {
        await _appService.InvalidateAllAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { result = InstalledResult });
    }

    [HttpPost]
    [EnableRateLimiting(GitHubPlugin.RateLimitPolicy)]
    public async Task<IActionResult> Create(GitHubManualAppDto dto, CancellationToken cancellationToken)
    {
        var result = await _appService.AddManualAsync(dto, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<string>.Success(result.Data, result.Message ?? "GitHub App eklendi."))
            : this.ApiFailure(result, "GitHub App eklenemedi.");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _appService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "GitHub App kaldırılamadı.");
    }

    [HttpPost]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        await _appService.InvalidateAllAsync(cancellationToken);
        return this.ApiSuccess("GitHub kurulum ve depo listeleri yenilendi.");
    }

    private string PanelBaseUrl =>
        string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}"
            : _options.PublicBaseUrl.TrimEnd('/');

    private async Task<IActionResult> IndexViewAsync(string? notice, bool isError, CancellationToken cancellationToken)
    {
        HttpContext.AllowFormAction(_options.WebUrl);
        var host = Request.Host.Host;
        var defaultName = $"Server Manager {host}";
        return View("Index", new GitHubIndexViewModel
        {
            Apps = await _appService.GetAppsAsync(cancellationToken),
            DefaultAppName = defaultName.Length > GitHubManifestRequestDtoValidator.MaxNameLength
                ? defaultName[..GitHubManifestRequestDtoValidator.MaxNameLength].TrimEnd()
                : defaultName,
            PanelBaseUrl = PanelBaseUrl,
            WebUrl = _options.WebUrl.TrimEnd('/'),
            Notice = notice,
            NoticeIsError = isError
        });
    }
}
