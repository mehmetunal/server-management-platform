using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class ProjectServiceLinkService : IProjectServiceLinkService
{
    private const string ProjectNotFoundMessage = "Proje bulunamadı.";
    private const string LinkNotFoundMessage = "Servis bağı bulunamadı.";

    private readonly IProjectServiceLinkRepository _links;
    private readonly IDeploymentRepository _projects;
    private readonly IManagedServiceService _services;
    private readonly IProjectEnvironmentService _environment;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProjectServiceLinkService> _logger;
    private readonly IServiceTemplateCatalog _templates;

    public ProjectServiceLinkService(
        IProjectServiceLinkRepository links,
        IDeploymentRepository projects,
        IManagedServiceService services,
        IProjectEnvironmentService environment,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ILogger<ProjectServiceLinkService> logger,
        IServiceTemplateCatalog templates)
    {
        _templates = templates;
        _links = links;
        _projects = projects;
        _services = services;
        _environment = environment;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ServiceResult<ServiceLinkPreviewDto>> GetPreviewAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var info = await _services.GetConnectionInfoAsync(serviceId, cancellationToken);
        if (!info.IsSuccess)
            return ServiceResult<ServiceLinkPreviewDto>.Failure(info.Message ?? "Servis bulunamadı.", info.ErrorType);

        var connection = info.Data!;
        var linked = (await _links.ListByServiceAsync(serviceId, cancellationToken)).Select(l => l.ProjectId).ToHashSet();
        var projects = new List<ServiceLinkProjectOptionDto>();
        foreach (var project in await _projects.GetProjectsByServerAsync(connection.ServerId, cancellationToken))
        {
            if (project.BuildType == DeploymentBuildType.Commands)
                continue;

            var keys = await _environment.GetEnvironmentKeysAsync(project.Id, cancellationToken);
            projects.Add(new ServiceLinkProjectOptionDto(project.Id, project.Name, project.BuildType, linked.Contains(project.Id), keys.Data ?? []));
        }

        return ServiceResult<ServiceLinkPreviewDto>.Success(new ServiceLinkPreviewDto
        {
            ServiceId = connection.ServiceId,
            ServiceName = connection.Name,
            ContainerName = connection.Host,
            Variables = connection.SuggestedEnvironmentPreview.Select(pair => new ServiceLinkVariablePreviewDto(pair.Key, pair.Value)).ToList(),
            Projects = projects
        });
    }

    public async Task<IReadOnlyList<ProjectServiceLinkDto>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        (await _links.ListByProjectAsync(projectId, cancellationToken)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ProjectServiceLinkDto>> ListForServiceAsync(Guid serviceId, CancellationToken cancellationToken = default) =>
        (await _links.ListByServiceAsync(serviceId, cancellationToken)).Select(ToDto).ToList();

    public async Task<ServiceResult<ServiceLinkResultDto>> LinkAsync(Guid serviceId, LinkServiceToProjectDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var info = await _services.GetConnectionInfoAsync(serviceId, cancellationToken);
        if (!info.IsSuccess)
            return ServiceResult<ServiceLinkResultDto>.Failure(info.Message ?? "Servis bulunamadı.", info.ErrorType);

        var connection = info.Data!;
        var project = await _projects.GetProjectAsync(dto.ProjectId, cancellationToken);
        if (project is null)
            return ServiceResult<ServiceLinkResultDto>.ValidationFailure(nameof(dto.ProjectId), ProjectNotFoundMessage);

        if (project.ServerId != connection.ServerId)
            return ServiceResult<ServiceLinkResultDto>.ValidationFailure(nameof(dto.ProjectId), "Servis yalnızca aynı sunucudaki projeye bağlanabilir.");

        if (project.BuildType == DeploymentBuildType.Commands)
        {
            return ServiceResult<ServiceLinkResultDto>.ValidationFailure(nameof(dto.ProjectId),
                "Komutla dağıtılan projenin container'ları panel tarafından yönetilmez; bağlantı bilgisini ortam değişkeni olarak elle ekleyin.");
        }

        if (await _links.FindAnyAsync(project.Id, serviceId, cancellationToken) is not null)
            return ServiceResult<ServiceLinkResultDto>.ValidationFailure(nameof(dto.ProjectId), "Bu servis projeye zaten bağlı.");

        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in dto.Variables.Where(v => v.Include))
        {
            var source = variable.SourceKey ?? string.Empty;
            if (!connection.SuggestedEnvironment.TryGetValue(source, out var value))
                return ServiceResult<ServiceLinkResultDto>.ValidationFailure("Variables", $"Bilinmeyen değişken: {source}");

            var key = string.IsNullOrWhiteSpace(variable.Key) ? source : variable.Key.Trim();
            if (!EnvironmentDocument.IsValidKey(key))
                return ServiceResult<ServiceLinkResultDto>.ValidationFailure("Variables", $"Geçersiz anahtar: {key}. Harf veya alt çizgiyle başlamalı; harf, rakam ve alt çizgi içermelidir.");

            if (!variables.TryAdd(key, value))
                return ServiceResult<ServiceLinkResultDto>.ValidationFailure("Variables", $"{key} anahtarı birden fazla kez seçildi.");
        }

        var change = new DTOs.Deployments.EnvironmentChangeResultDto();
        if (variables.Count > 0)
        {
            var upsert = await _environment.UpsertEnvironmentVariablesAsync(project.Id, variables, dto.Overwrite, cancellationToken);
            if (!upsert.IsSuccess)
                return ServiceResult<ServiceLinkResultDto>.Failure(upsert.Message ?? "Ortam değişkenleri yazılamadı.", upsert.ErrorType);

            change = upsert.Data!;
        }

        var written = change.Added.Concat(change.Updated).ToList();
        var link = new ProjectServiceLink
        {
            ProjectId = project.Id,
            ManagedServiceId = serviceId,
            EnvironmentKeys = written.Count == 0 ? null : TextHelper.Truncate(string.Join(',', written), 2000),
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            CreatedBy = _currentUser.UserName
        };
        await _links.AddAsync(link, cancellationToken);
        await _links.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Servis projeye bağlandı. ServiceId: {ServiceId}, ProjectId: {ProjectId}", serviceId, project.Id);
        var details = $"Servis: {connection.Name} ({connection.Host})"
                      + (written.Count > 0 ? $" | Yazılan: {string.Join(", ", written)}" : string.Empty)
                      + (change.Skipped.Count > 0 ? $" | Korunan: {string.Join(", ", change.Skipped)}" : string.Empty);
        await AuditAsync(AuditActions.ProjectServiceLink, project, details, cancellationToken);

        return ServiceResult<ServiceLinkResultDto>.Success(new ServiceLinkResultDto
        {
            LinkId = link.Id,
            ProjectId = project.Id,
            ProjectName = project.Name,
            Change = change
        }, Describe(project.Name, change));
    }

    public async Task<ServiceResult> UnlinkAsync(Guid linkId, bool removeKeys, CancellationToken cancellationToken = default)
    {
        var link = await _links.GetAsync(linkId, cancellationToken);
        if (link?.Project is null || link.ManagedService is null)
            return ServiceResult.NotFound(LinkNotFoundMessage);

        var keys = SplitKeys(link.EnvironmentKeys);
        IReadOnlyList<string> removed = [];
        if (removeKeys && keys.Count > 0)
        {
            var result = await _environment.RemoveEnvironmentVariablesAsync(link.ProjectId, keys, cancellationToken);
            if (!result.IsSuccess)
                return ServiceResult.Failure(result.Message ?? "Ortam değişkenleri silinemedi.", result.ErrorType);

            removed = result.Data!.Removed;
        }

        var project = link.Project;
        var serviceName = link.ManagedService.Name;
        var container = link.ManagedService.ContainerName;
        _links.Remove(link);
        await _links.SaveChangesAsync(cancellationToken);

        var details = $"Servis: {serviceName} ({container})" + (removed.Count > 0 ? $" | Silinen değişkenler: {string.Join(", ", removed)}" : " | Ortam değişkenleri korundu");
        await AuditAsync(AuditActions.ProjectServiceUnlink, project, details, cancellationToken);
        return ServiceResult.Success(removed.Count > 0
            ? $"Bağ kaldırıldı; {removed.Count} ortam değişkeni silindi. Değişiklik bir sonraki deploy veya yeniden başlatmada uygulanır."
            : "Bağ kaldırıldı; ortam değişkenleri korundu. Ağ değişikliği bir sonraki deploy veya yeniden başlatmada uygulanır.");
    }

    private static string Describe(string projectName, DTOs.Deployments.EnvironmentChangeResultDto change)
    {
        var parts = new List<string> { $"Servis {projectName} projesine bağlandı." };
        if (change.Added.Count > 0)
            parts.Add($"Eklenen: {string.Join(", ", change.Added)}.");
        if (change.Updated.Count > 0)
            parts.Add($"Değişen: {string.Join(", ", change.Updated)}.");
        if (change.Skipped.Count > 0)
            parts.Add($"Zaten tanımlı olduğu için korunan: {string.Join(", ", change.Skipped)}.");
        return string.Join(' ', parts);
    }

    private static IReadOnlyList<string> SplitKeys(string? keys) =>
        (keys ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private ProjectServiceLinkDto ToDto(ProjectServiceLink link)
    {
        var template = _templates.Resolve(link.ManagedService?.TemplateKey).Template;
        return new ProjectServiceLinkDto
        {
            Id = link.Id,
            ProjectId = link.ProjectId,
            ProjectName = link.Project?.Name ?? string.Empty,
            ServiceId = link.ManagedServiceId,
            ServiceName = link.ManagedService?.Name ?? string.Empty,
            TemplateKey = link.ManagedService?.TemplateKey ?? string.Empty,
            TemplateName = template?.DisplayName ?? link.ManagedService?.TemplateKey ?? string.Empty,
            ContainerName = link.ManagedService?.ContainerName ?? string.Empty,
            ServiceStatus = link.ManagedService?.Status ?? ManagedServiceStatus.Failed,
            EnvironmentKeys = SplitKeys(link.EnvironmentKeys),
            CreatedAt = link.CreatedAt,
            CreatedBy = link.CreatedBy
        };
    }

    private Task AuditAsync(string action, DeploymentProject project, string details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.Project,
            project.Id.ToString(),
            project.Name,
            TextHelper.Truncate(details, 2000)), cancellationToken);
}
