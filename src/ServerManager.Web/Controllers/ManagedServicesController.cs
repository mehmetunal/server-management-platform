using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Deployments;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.ManagedServices;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

/// <summary>Servisler: sunuculara tek tıkla kurulan veritabanı ve uygulama servisleri.</summary>
[HasPermission(Permissions.ServicesView)]
public partial class ManagedServicesController : Controller
{
    private readonly IManagedServiceService _services;
    private readonly IServerService _serverService;
    private readonly ManagedServiceManager _manager;
    private readonly ICurrentUserService _currentUser;
    private readonly ManagedServiceOptions _options;
    private readonly IProjectServiceLinkService _links;
    private readonly IManagedServiceBackupService _backups;
    private readonly IBackupStorageService _storages;
    private readonly DeploymentManager _deployments;
    private readonly IServiceTemplateCatalog _templates;

    public ManagedServicesController(
        IManagedServiceService services,
        IServerService serverService,
        ManagedServiceManager manager,
        ICurrentUserService currentUser,
        IOptions<ManagedServiceOptions> options,
        IProjectServiceLinkService links,
        IManagedServiceBackupService backups,
        IBackupStorageService storages,
        DeploymentManager deployments,
        IServiceTemplateCatalog templates)
    {
        _templates = templates;
        _services = services;
        _serverService = serverService;
        _manager = manager;
        _currentUser = currentUser;
        _options = options.Value;
        _links = links;
        _backups = backups;
        _storages = storages;
        _deployments = deployments;
    }

    private ServiceActor Actor => new(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);

    [HttpGet]
    public async Task<IActionResult> Index(Guid? serverId, CancellationToken cancellationToken)
    {
        var services = await _services.ListAsync(serverId, cancellationToken);
        return View(new ManagedServiceIndexViewModel
        {
            Services = services,
            Servers = await _serverService.GetOptionsAsync(cancellationToken),
            ServerId = serverId
        });
    }

    [HttpGet]
    [HasPermission(Permissions.ServicesManage)]
    public async Task<IActionResult> Create(Guid? serverId, string? template, CancellationToken cancellationToken)
    {
        var selected = _templates.Find(template);
        var form = new CreateManagedServiceDto { ServerId = serverId ?? Guid.Empty };
        if (selected is not null)
        {
            form.TemplateKey = selected.Key;
            form.Name = selected.LocalKey;
            form.ImageTag = selected.DefaultTag;
            form.Username = selected.Credentials.DefaultUsername;
            form.Database = selected.Credentials.DefaultDatabase;
            form.Password = selected.Credentials.HasPassword ? ServiceSecrets.GeneratePassword() : null;
            form.Ports = selected.Ports
                .Select(p => new ServicePortFormItem { ContainerPort = p.ContainerPort, Publish = p.PublishByDefault, HostPort = p.ContainerPort })
                .ToList();
        }

        var canBackup = selected is not null && ManagedServiceBackups.Supports(selected.Key) && User.HasPermission(Permissions.BackupManage);
        return View(new ManagedServiceCreateViewModel
        {
            Template = selected,
            Templates = selected is null ? _templates.GetAvailable() : [],
            Categories = selected is null ? _templates.GetCategories() : [],
            Form = form,
            Servers = await _serverService.GetOptionsAsync(cancellationToken),
            AllowPrivilegedPorts = _options.AllowPrivilegedHostPorts,
            CanConfigureBackup = canBackup,
            BackupStorages = canBackup ? await _storages.GetOptionsAsync(cancellationToken) : []
        });
    }

    [HttpPost]
    [HasPermission(Permissions.ServicesManage)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> Create(
        CreateManagedServiceDto dto,
        [Bind(Prefix = "AutoBackup")] ServiceBackupOptionsDto? autoBackup,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        ServiceBackupOptionsDto? afterInstall = null;
        if (autoBackup is { Enabled: true })
        {
            if (!User.HasPermission(Permissions.BackupManage))
                return Forbid();

            var check = await _backups.ValidateOptionsAsync(dto.TemplateKey, dto.Database, autoBackup, cancellationToken);
            if (!check.IsSuccess)
                return this.ApiFailure(check, "Otomatik yedek ayarları geçersiz.");

            afterInstall = autoBackup;
        }

        var actor = Actor;
        var result = await _manager.StartAsync(null, actor, (service, ct) => service.BeginInstallAsync(dto, actor, ct), cancellationToken, afterInstall);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Operation), new { id = result.Data!.OperationId }))
            : this.ApiFailure(result, "Servis kurulamadı.");
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, [FromServices] IAlertService alerts, CancellationToken cancellationToken)
    {
        var service = await _services.GetAsync(id, cancellationToken);
        if (!service.IsSuccess)
            return NotFound();

        // Şablon eklentisi devre dışıysa Ayarlar ve Sürüm sekmeleri gösterilmez (yeniden oluşturma/yükseltme engellidir).
        UpdateManagedServiceDto? settings = null;
        if (User.HasPermission(Permissions.ServicesManage) && service.Data!.CanChangeTemplateSettings)
            settings = (await _services.GetSettingsAsync(id, cancellationToken)).Data;

        var details = service.Data!;
        var canBackup = User.HasPermission(Permissions.BackupManage) && ManagedServiceBackups.Supports(details.TemplateKey)
                        && details.Status != ManagedServiceStatus.Removed;
        return View(new ManagedServiceDetailsViewModel
        {
            Service = details,
            Operations = await _services.ListOperationsAsync(id, cancellationToken),
            Settings = settings,
            AllowPrivilegedPorts = _options.AllowPrivilegedHostPorts,
            Links = await _links.ListForServiceAsync(id, cancellationToken),
            CanLink = CanLink && details.Status != ManagedServiceStatus.Removed && details.Template?.PrimaryPort is not null,
            CanBackup = canBackup,
            BackupJobs = canBackup ? await _backups.ListJobsAsync(id, cancellationToken) : [],
            BackupStorages = canBackup ? await _storages.GetOptionsAsync(cancellationToken) : [],
            ActiveAlerts = User.HasPermission(Permissions.AlertView)
                ? await alerts.GetOpenServiceAlertsAsync(details.Id, details.ServerId, details.ContainerName, cancellationToken)
                : null,
            CanManageAlerts = User.HasPermission(Permissions.AlertManage)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Operation(Guid id, CancellationToken cancellationToken)
    {
        var operation = await _services.GetOperationAsync(id, includeLog: false, cancellationToken);
        if (!operation.IsSuccess)
            return NotFound();

        var service = await _services.GetAsync(operation.Data!.ServiceId, cancellationToken);
        return View(new ManagedServiceOperationViewModel { Operation = operation.Data, Service = service.Data });
    }

    [HttpGet]
    public async Task<IActionResult> OperationLog(Guid id, CancellationToken cancellationToken)
    {
        var operation = await _services.GetOperationAsync(id, includeLog: true, cancellationToken);
        if (!operation.IsSuccess)
            return NotFound();

        var data = operation.Data!;
        var text = AnsiPattern().Replace(data.Log, string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var fileName = $"servis-{data.ServiceName}-{data.Kind}-{data.StartedAt:yyyyMMdd-HHmmss}.log".ToLowerInvariant();
        return File(Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> Probe(Guid serverId, CancellationToken cancellationToken) =>
        DataResult(await _services.ProbeServerAsync(serverId, cancellationToken), "Sunucu bilgisi alınamadı.");

    [HttpGet]
    [HasPermission(Permissions.ServicesManage)]
    public async Task<IActionResult> Networks(Guid serverId, CancellationToken cancellationToken) =>
        DataResult(await _services.ListServerNetworksAsync(serverId, cancellationToken), "Docker ağları alınamadı.");

    [HttpGet]
    public async Task<IActionResult> Runtime(Guid id, CancellationToken cancellationToken) =>
        DataResult(await _services.GetRuntimeAsync(id, cancellationToken), "Container durumu alınamadı.");

    [HttpGet]
    public async Task<IActionResult> Logs(Guid serviceId, int? tail, string? since, CancellationToken cancellationToken) =>
        DataResult(await _services.GetLogsAsync(serviceId, tail, since, cancellationToken), "Loglar alınamadı.");

    [HttpPost]
    [HasPermission(Permissions.ServicesManage)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> ContainerAction(Guid id, DockerContainerAction action, CancellationToken cancellationToken)
    {
        var result = await _services.ExecuteContainerActionAsync(id, action, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "İşlem başarısız.");
    }

    [HttpPost]
    [HasPermission(Permissions.ServicesRevealSecrets)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> Reveal(Guid id, CancellationToken cancellationToken)
    {
        var result = await _services.RevealSecretsAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kimlik bilgileri gösterilemedi.");

        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResponse<ManagedServiceSecretsDto>.Success(result.Data, "Kimlik bilgileri gösterildi; işlem audit log'a yazıldı."));
    }

    [HttpPost]
    [HasPermission(Permissions.ServicesManage)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> Settings(Guid id, UpdateManagedServiceDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var actor = Actor;
        var result = await _manager.StartAsync(id, actor, (service, ct) => service.BeginRecreateAsync(id, dto, actor, ct), cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Operation), new { id = result.Data!.OperationId }))
            : this.ApiFailure(result, "Ayarlar kaydedilemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.ServicesManage)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> Upgrade(Guid id, UpgradeManagedServiceDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var actor = Actor;
        var result = await _manager.StartAsync(id, actor, (service, ct) => service.BeginUpgradeAsync(id, dto, actor, ct), cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Operation), new { id = result.Data!.OperationId }))
            : this.ApiFailure(result, "Sürüm yükseltilemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.ServicesManage)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> Remove(Guid id, RemoveManagedServiceDto dto, CancellationToken cancellationToken)
    {
        var actor = Actor;
        var result = await _manager.StartAsync(id, actor, (service, ct) => service.BeginRemoveAsync(id, dto, actor, ct), cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message, Url.Action(nameof(Operation), new { id = result.Data!.OperationId }))
            : this.ApiFailure(result, "Servis kaldırılamadı.");
    }

    [HttpPost]
    [HasPermission(Permissions.ServicesManage)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> ReapplyFirewall(Guid id, CancellationToken cancellationToken)
    {
        var result = await _services.ReapplyFirewallAsync(id, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Güvenlik duvarı kuralları uygulanamadı.");
    }

    // ---- Projeye bağla

    private bool CanLink => User.HasPermission(Permissions.ServicesManage) && User.HasPermission(Permissions.DeploymentManage);

    [HttpGet]
    [HasPermission(Permissions.ServicesManage)]
    [HasPermission(Permissions.DeploymentManage)]
    public async Task<IActionResult> LinkPreview(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return DataResult(await _links.GetPreviewAsync(id, cancellationToken), "Bağlantı bilgisi alınamadı.");
    }

    [HttpPost]
    [HasPermission(Permissions.ServicesManage)]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> Link(Guid id, LinkServiceToProjectDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        if (dto.ApplyNow && !User.HasPermission(Permissions.DeploymentExecute))
            return this.ApiFailure(ServiceResult.ValidationFailure(nameof(dto.ApplyNow), "Hemen uygulamak için deployment başlatma yetkiniz yok; bağlayıp projeyi daha sonra yeniden başlatın."), "Servis bağlanamadı.");

        var result = await _links.LinkAsync(id, dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Servis bağlanamadı.");

        if (!dto.ApplyNow)
            return this.ApiSuccess(result.Message + " Ağ ve ortam değişkenleri bir sonraki deploy veya \"Uygula / Yeniden başlat\" ile uygulanır.");

        var actor = new DeploymentActor(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);
        var restart = await _deployments.RestartAsync(result.Data!.ProjectId, actor, cancellationToken);
        return restart.IsSuccess
            ? this.ApiSuccess(result.Message + " Proje yeniden başlatılıyor.", Url.Action("Details", "Deployments", new { id = restart.Data }))
            : this.ApiSuccess(result.Message + " Yeniden başlatılamadı: " + (restart.Message ?? "bilinmeyen hata") + " Proje sayfasından uygulayın.");
    }

    [HttpPost]
    [HasPermission(Permissions.ServicesManage)]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> Unlink(Guid linkId, bool removeKeys, CancellationToken cancellationToken)
    {
        var result = await _links.UnlinkAsync(linkId, removeKeys, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Bağ kaldırılamadı.");
    }

    // ---- Yedekleme

    [HttpPost]
    [HasPermission(Permissions.BackupManage)]
    [EnableRateLimiting(RateLimitPolicies.ServiceAction)]
    public async Task<IActionResult> CreateBackupJob(Guid id, [Bind(Prefix = "AutoBackup")] ServiceBackupOptionsDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _backups.CreateJobAsync(id, dto, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message ?? "Yedekleme işi oluşturuldu.", Url.Action("Details", "BackupJobs", new { id = result.Data }))
            : this.ApiFailure(result, "Yedekleme işi oluşturulamadı.");
    }

    private IActionResult DataResult<T>(ServiceResult<T> result, string fallback)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse<T>.Success(result.Data));

        var statusCode = ApiResultExtensions.StatusCodeFor(result.ErrorType);
        return StatusCode(statusCode, ApiResponse<T>.Fail(result.Errors.FirstOrDefault()?.Message ?? result.Message ?? fallback, statusCode));
    }

    [GeneratedRegex(@"\u001b\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiPattern();
}
