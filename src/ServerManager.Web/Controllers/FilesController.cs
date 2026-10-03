using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Files;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Authorization;
using ServerManager.Web.Extensions;
using ServerManager.Web.Filters;
using ServerManager.Web.Middleware;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;
using ServerManager.Web.Services;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.FileView)]
public class FilesController : Controller
{
    private readonly IFileService _fileService;
    private readonly ServerPageBuilder _pageBuilder;
    private readonly FileManagerOptions _options;

    public FilesController(IFileService fileService, ServerPageBuilder pageBuilder, IOptions<FileManagerOptions> options)
    {
        _fileService = fileService;
        _pageBuilder = pageBuilder;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, string? path, CancellationToken cancellationToken)
    {
        ServerPageViewModel? page = null;
        if (!Request.IsAjax())
        {
            page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.FilesTab, MetricRange.OneHour, cancellationToken);
            if (page is null)
                return NotFound();
        }

        var listing = await _fileService.ListAsync(id, path, cancellationToken);
        var list = new FileListViewModel
        {
            ServerId = id,
            Listing = listing.Data,
            Error = listing.IsSuccess ? null : FirstMessage(listing) ?? "Klasör listelenemedi.",
            RequestedPath = path
        };

        if (page is null)
            return PartialView("_FileList", list);

        return View(new FileManagerViewModel { Page = page, List = list, MaxUploadMegabytes = _options.MaxUploadMegabytes });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, string? path, CancellationToken cancellationToken)
    {
        var normalized = RemotePath.Normalize(path);
        if (normalized is null || normalized == "/")
            return RedirectToAction(nameof(Index), new { id });

        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.FilesTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        return View(new FileEditorViewModel
        {
            Page = page,
            Path = normalized,
            FileName = RemotePath.GetFileName(normalized),
            DirectoryPath = RemotePath.GetParent(normalized) ?? "/",
            MaxEditKilobytes = _options.MaxEditKilobytes
        });
    }

    [HttpGet]
    public async Task<IActionResult> Read(Guid id, string? path, CancellationToken cancellationToken) =>
        DataResult(await _fileService.ReadAsync(id, path ?? string.Empty, cancellationToken), "Dosya okunamadı.");

    [HttpPost]
    [HasPermission(Permissions.FileEdit)]
    [EnableRateLimiting(RateLimitPolicies.FileAction)]
    public async Task<IActionResult> Save(Guid id, SaveFileDto dto, CancellationToken cancellationToken) =>
        DataResult(await _fileService.SaveAsync(id, dto, cancellationToken), "Dosya kaydedilemedi.");

    [HttpPost]
    [HasPermission(Permissions.FileCreate)]
    [EnableRateLimiting(RateLimitPolicies.FileAction)]
    public async Task<IActionResult> Create(Guid id, CreateFileEntryDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _fileService.CreateAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.FileEdit)]
    [EnableRateLimiting(RateLimitPolicies.FileAction)]
    public async Task<IActionResult> Move(Guid id, MoveFileDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _fileService.MoveAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.FileCreate)]
    [EnableRateLimiting(RateLimitPolicies.FileAction)]
    public async Task<IActionResult> Copy(Guid id, CopyFileDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _fileService.CopyAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.FileDelete)]
    [EnableRateLimiting(RateLimitPolicies.FileAction)]
    public async Task<IActionResult> Delete(Guid id, DeleteFileDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _fileService.DeleteAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.FilePermissions)]
    [EnableRateLimiting(RateLimitPolicies.FileAction)]
    public async Task<IActionResult> ChangePermissions(Guid id, ChangePermissionsDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _fileService.ChangePermissionsAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.FileUpload)]
    [EnableRateLimiting(RateLimitPolicies.FileAction)]
    [FileUploadRequest]
    public async Task<IActionResult> Upload(Guid id, CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
            return BadRequest(ApiResponse<string>.Fail("Geçersiz yükleme isteği.", StatusCodes.Status400BadRequest));

        IFormCollection form;
        try
        {
            form = await Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = FileUploadRequestAttribute.MaxBodyBytes(_options) }, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, ApiResponse<string>.Fail(
                $"Dosya en fazla {_options.MaxUploadMegabytes} MB olabilir.", StatusCodes.Status413PayloadTooLarge));
        }

        var file = form.Files.GetFile("file");
        if (file is null)
            return BadRequest(ApiResponse<string>.Fail("Yüklenecek dosya seçilmedi.", StatusCodes.Status400BadRequest));

        var dto = new UploadFileDto
        {
            Directory = form["Directory"].ToString(),
            FileName = string.IsNullOrWhiteSpace(form["FileName"]) ? Path.GetFileName(file.FileName) : form["FileName"].ToString(),
            Overwrite = bool.TryParse(form["Overwrite"], out var overwrite) && overwrite
        };

        await using var content = file.OpenReadStream();
        return ActionResultFor(await _fileService.UploadAsync(id, dto, content, file.Length, cancellationToken));
    }

    [HttpGet]
    [HasPermission(Permissions.FileDownload)]
    public async Task<IActionResult> Download(Guid id, string? path, CancellationToken cancellationToken)
    {
        var result = await _fileService.DownloadAsync(id, path ?? string.Empty, cancellationToken);
        if (!result.IsSuccess)
        {
            HttpContext.AllowSameOriginFraming();
            Response.StatusCode = ApiResultExtensions.StatusCodeFor(result.ErrorType);
            return Content(FirstMessage(result) ?? "Dosya indirilemedi.", "text/plain; charset=utf-8");
        }

        var download = result.Data!;
        return File(download.Content, "application/octet-stream", download.FileName);
    }

    private IActionResult DataResult<T>(ServiceResult<T> result, string fallbackMessage)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse<T>.Success(result.Data, result.Message ?? string.Empty));

        return this.ApiFailure(result, fallbackMessage);
    }

    private IActionResult ActionResultFor(ServiceResult result) =>
        result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "İşlem başarısız.");

    private static string? FirstMessage(ServiceResult result) =>
        result.Errors.FirstOrDefault()?.Message ?? result.Message;
}
