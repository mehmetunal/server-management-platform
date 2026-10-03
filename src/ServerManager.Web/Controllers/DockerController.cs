using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Authorization;
using ServerManager.Web.Extensions;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;
using ServerManager.Web.Services;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.DockerView)]
public class DockerController : Controller
{
    private const string PanelErrorView = "_PanelError";

    private readonly IDockerService _dockerService;
    private readonly ServerPageBuilder _pageBuilder;

    public DockerController(IDockerService dockerService, ServerPageBuilder pageBuilder)
    {
        _dockerService = dockerService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public Task<IActionResult> Index(Guid id, CancellationToken cancellationToken) =>
        SectionPageAsync(id, DockerPageViewModel.OverviewSection, cancellationToken);

    [HttpGet]
    public Task<IActionResult> Containers(Guid id, CancellationToken cancellationToken) =>
        SectionPageAsync(id, DockerPageViewModel.ContainersSection, cancellationToken);

    [HttpGet]
    public Task<IActionResult> Images(Guid id, CancellationToken cancellationToken) =>
        SectionPageAsync(id, DockerPageViewModel.ImagesSection, cancellationToken);

    [HttpGet]
    public Task<IActionResult> Volumes(Guid id, CancellationToken cancellationToken) =>
        SectionPageAsync(id, DockerPageViewModel.VolumesSection, cancellationToken);

    [HttpGet]
    public Task<IActionResult> Networks(Guid id, CancellationToken cancellationToken) =>
        SectionPageAsync(id, DockerPageViewModel.NetworksSection, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> Container(Guid id, string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || !DockerNames.IsValidContainerReference(name))
            return RedirectToAction(nameof(Containers), new { id });

        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.DockerTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        return View(new DockerPageViewModel { Page = page, Section = DockerPageViewModel.ContainersSection, Container = name });
    }

    [HttpGet]
    public async Task<IActionResult> OverviewPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dockerService.GetOverviewAsync(id, cancellationToken);
        return PanelResult(id, result, "_OverviewPanel");
    }

    [HttpGet]
    public async Task<IActionResult> ContainersPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dockerService.GetContainersAsync(id, cancellationToken);
        return PanelResult(id, result, "_ContainersPanel");
    }

    [HttpGet]
    public async Task<IActionResult> ContainerPanel(Guid id, string? name, CancellationToken cancellationToken)
    {
        var result = await _dockerService.GetContainerAsync(id, name ?? string.Empty, cancellationToken);
        return PanelResult(id, result, "_ContainerPanel");
    }

    [HttpGet]
    public async Task<IActionResult> ImagesPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dockerService.GetImagesAsync(id, cancellationToken);
        return PanelResult(id, result, "_ImagesPanel");
    }

    [HttpGet]
    public async Task<IActionResult> VolumesPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dockerService.GetVolumesAsync(id, cancellationToken);
        return PanelResult(id, result, "_VolumesPanel");
    }

    [HttpGet]
    public async Task<IActionResult> NetworksPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dockerService.GetNetworksAsync(id, cancellationToken);
        return PanelResult(id, result, "_NetworksPanel");
    }

    [HttpGet]
    public async Task<IActionResult> ContainerStats(Guid id, string? container, CancellationToken cancellationToken)
    {
        var result = await _dockerService.GetContainerStatsAsync(id, container, cancellationToken);
        return DataResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> ContainerLogs(Guid id, DockerLogQuery query, CancellationToken cancellationToken)
    {
        var result = await _dockerService.GetContainerLogsAsync(id, query, cancellationToken);
        return DataResult(result);
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.DockerAction)]
    public async Task<IActionResult> ContainerAction(Guid id, ContainerActionRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Action))
            return BadRequest(ApiResponse<object>.Fail("Geçersiz işlem.", StatusCodes.Status400BadRequest));

        if (!User.HasPermission(DockerActionPolicies.RequiredPermission(request.Action)))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Bu işlem için yetkiniz yok.", StatusCodes.Status403Forbidden));

        var result = await _dockerService.ExecuteContainerActionAsync(id, request, cancellationToken);
        return ActionResultFor(result);
    }

    [HttpPost]
    [HasPermission(Permissions.DockerManage)]
    [EnableRateLimiting(RateLimitPolicies.DockerAction)]
    public async Task<IActionResult> RenameContainer(Guid id, RenameContainerDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _dockerService.RenameContainerAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.DockerManage)]
    [EnableRateLimiting(RateLimitPolicies.DockerAction)]
    public async Task<IActionResult> PullImage(Guid id, PullImageDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _dockerService.PullImageAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.DockerDelete)]
    [EnableRateLimiting(RateLimitPolicies.DockerAction)]
    public async Task<IActionResult> RemoveImage(Guid id, string? reference, bool force, CancellationToken cancellationToken) =>
        ActionResultFor(await _dockerService.RemoveImageAsync(id, reference ?? string.Empty, force, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.DockerDelete)]
    [EnableRateLimiting(RateLimitPolicies.DockerAction)]
    public async Task<IActionResult> PruneImages(Guid id, bool all, string? confirmationName, CancellationToken cancellationToken) =>
        ActionResultFor(await _dockerService.PruneImagesAsync(id, all, confirmationName, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.DockerManage)]
    [EnableRateLimiting(RateLimitPolicies.DockerAction)]
    public async Task<IActionResult> CreateVolume(Guid id, CreateVolumeDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _dockerService.CreateVolumeAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.DockerDelete)]
    [EnableRateLimiting(RateLimitPolicies.DockerAction)]
    public async Task<IActionResult> RemoveVolume(Guid id, string? name, string? confirmationName, CancellationToken cancellationToken) =>
        ActionResultFor(await _dockerService.RemoveVolumeAsync(id, name ?? string.Empty, confirmationName, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.DockerManage)]
    [EnableRateLimiting(RateLimitPolicies.DockerAction)]
    public async Task<IActionResult> CreateNetwork(Guid id, CreateNetworkDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _dockerService.CreateNetworkAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.DockerDelete)]
    [EnableRateLimiting(RateLimitPolicies.DockerAction)]
    public async Task<IActionResult> RemoveNetwork(Guid id, string? name, CancellationToken cancellationToken) =>
        ActionResultFor(await _dockerService.RemoveNetworkAsync(id, name ?? string.Empty, cancellationToken));

    private async Task<IActionResult> SectionPageAsync(Guid id, string section, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.DockerTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        return View("Section", new DockerPageViewModel { Page = page, Section = section });
    }

    private IActionResult PanelResult<T>(Guid serverId, ServiceResult<T> result, string viewName)
    {
        if (!result.IsSuccess)
        {
            Response.StatusCode = result.ErrorType == ServiceErrorType.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status200OK;
            return PartialView(PanelErrorView, FirstMessage(result) ?? "Docker bilgisi alınamadı.");
        }

        return PartialView(viewName, new DockerPanelModel<T> { ServerId = serverId, Data = result.Data! });
    }

    private IActionResult DataResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse<T>.Success(result.Data));

        var statusCode = ApiResultExtensions.StatusCodeFor(result.ErrorType);
        return StatusCode(statusCode, ApiResponse<T>.Fail(FirstMessage(result) ?? "Docker bilgisi alınamadı.", statusCode));
    }

    private IActionResult ActionResultFor(ServiceResult result) =>
        result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "İşlem başarısız.");

    private static string? FirstMessage(ServiceResult result) =>
        result.Errors.FirstOrDefault()?.Message ?? result.Message;
}
