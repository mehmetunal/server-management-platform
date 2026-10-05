using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class DeploymentDomainService : IDeploymentDomainService
{
    private const string ProjectNotFound = "Proje bulunamadı.";
    private const string DomainNotFound = "Domain bulunamadı.";
    private const string DeploymentRunning = "Proje için süren bir deployment var; bitmesini bekleyin veya iptal edin.";

    private readonly IDeploymentRepository _projects;
    private readonly IDeploymentDomainRepository _domains;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IDeploymentProvider _provider;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<DomainFormDto> _validator;
    private readonly DeploymentOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeploymentDomainService> _logger;

    public DeploymentDomainService(
        IDeploymentRepository projects,
        IDeploymentDomainRepository domains,
        IServerConnectionProvider connectionProvider,
        IDeploymentProvider provider,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<DomainFormDto> validator,
        IOptions<DeploymentOptions> options,
        TimeProvider timeProvider,
        ILogger<DeploymentDomainService> logger)
    {
        _projects = projects;
        _domains = domains;
        _connectionProvider = connectionProvider;
        _provider = provider;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<DomainListItemDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var domains = await _domains.ListByProjectAsync(projectId, cancellationToken);
        return domains.Select(ToListItem).ToList();
    }

    public async Task<ServiceResult<ProxyStatusDto>> GetProxyStatusAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<ProxyStatusDto>.NotFound(ProjectNotFound);

        var connection = await _connectionProvider.GetAsync(project.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<ProxyStatusDto>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await _provider.GetProxyStatusAsync(connection.Data!.Context, cancellationToken);
    }

    public async Task<ServiceResult> InstallProxyAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!DomainNames.IsAcmeEmail(_options.AcmeEmail))
            return ServiceResult.Failure("Let's Encrypt e-postası yapılandırılmamış (Deployment:AcmeEmail).");

        var project = await _projects.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult.NotFound(ProjectNotFound);

        var connection = await _connectionProvider.GetAsync(project.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var result = await _provider.InstallProxyAsync(
            connection.Data!.Context,
            _options.AcmeEmail.Trim(),
            TimeSpan.FromMinutes(Math.Max(1, _options.DeployTimeoutMinutes)),
            cancellationToken);

        await AuditAsync(AuditActions.DeploymentProxyInstall, project, result.Message, result.IsSuccess, cancellationToken);
        _logger.LogInformation("Vekil kurulumu tamamlandı. ServerId: {ServerId}, Success: {Success}", project.ServerId, result.IsSuccess);
        return result;
    }

    public async Task<ServiceResult> SaveAsync(Guid projectId, DomainFormDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var project = await _projects.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult.NotFound(ProjectNotFound);

        if (project.BuildType == DeploymentBuildType.Commands)
            return ServiceResult.Failure("Komutla dağıtılan projeye domain bağlanamaz. Docker Compose veya Dockerfile kullanın.");

        if (await IsDeployingAsync(project, cancellationToken))
            return ServiceResult.Failure(DeploymentRunning, ServiceErrorType.Conflict);

        DomainNames.TryNormalizeHost(dto.Host, out var host, out _);
        DomainNames.TryNormalizePath(dto.Path, out var path, out _);
        var serviceName = string.IsNullOrWhiteSpace(dto.ServiceName) ? null : dto.ServiceName.Trim();
        if (project.BuildType == DeploymentBuildType.DockerCompose && !DomainNames.IsValidServiceName(serviceName))
            return ServiceResult.ValidationFailure(nameof(DomainFormDto.ServiceName), "Compose servis adını girin (ör. web).");

        var creating = dto.Id is null || dto.Id == Guid.Empty;
        if (await _domains.HostPathExistsAsync(project.ServerId, host, path, creating ? null : dto.Id, cancellationToken))
            return ServiceResult.Failure("Bu sunucuda aynı host ve yol başka bir domaine ait.", ServiceErrorType.Conflict);

        DeploymentDomain domain;
        if (creating)
        {
            if (await _domains.CountByProjectAsync(projectId, cancellationToken) >= DomainNames.MaxDomainsPerProject)
                return ServiceResult.Failure($"Bir projeye en fazla {DomainNames.MaxDomainsPerProject} domain eklenebilir.");

            domain = new DeploymentDomain
            {
                ProjectId = project.Id,
                ServerId = project.ServerId,
                CreatedAt = UtcNow,
                CreatedBy = _currentUser.UserName
            };
            await _domains.AddAsync(domain, cancellationToken);
        }
        else
        {
            var existing = await _domains.GetAsync(dto.Id!.Value, cancellationToken);
            if (existing is null || existing.ProjectId != project.Id)
                return ServiceResult.NotFound(DomainNotFound);

            if (dto.TlsMode == DeploymentTlsMode.Custom
                && string.IsNullOrWhiteSpace(dto.CertificatePem)
                && (existing.EncryptedCertificate is null || existing.EncryptedPrivateKey is null))
            {
                return ServiceResult.ValidationFailure(nameof(DomainFormDto.CertificatePem), "Özel sertifikaya geçmek için sertifika ve özel anahtarı birlikte yapıştırın.");
            }

            domain = existing;
            domain.UpdatedAt = UtcNow;
            domain.UpdatedBy = _currentUser.UserName;
        }

        domain.Host = host;
        domain.Path = path;
        domain.ContainerPort = dto.ContainerPort;
        domain.ServiceName = serviceName;
        domain.TlsMode = dto.TlsMode;
        var secrets = ApplySecrets(domain, dto, creating);
        if (!secrets.IsSuccess)
            return secrets;

        await _domains.SaveChangesAsync(cancellationToken);
        dto.ClearSecrets();
        var action = creating ? AuditActions.DeploymentDomainCreate : AuditActions.DeploymentDomainUpdate;
        await AuditAsync(action, project, $"{host}{path} · {dto.TlsMode} · port {dto.ContainerPort}", true, cancellationToken);

        var applied = await ApplyProjectAsync(project, cancellationToken);
        return ServiceResult.Success(applied.IsSuccess
            ? "Domain kaydedildi ve yönlendirme uygulandı."
            : "Domain kaydedildi. " + applied.Message);
    }

    public async Task<ServiceResult> DeleteAsync(Guid projectId, Guid domainId, string? confirmationHost, CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult.NotFound(ProjectNotFound);

        var domain = await _domains.GetAsync(domainId, cancellationToken);
        if (domain is null || domain.ProjectId != project.Id)
            return ServiceResult.NotFound(DomainNotFound);

        if (!string.Equals(confirmationHost?.Trim(), domain.Host, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.ValidationFailure("ConfirmationHost", "Silmek için host adını birebir yazın.");

        if (await IsDeployingAsync(project, cancellationToken))
            return ServiceResult.Failure(DeploymentRunning, ServiceErrorType.Conflict);

        domain.IsDeleted = true;
        domain.DeletedAt = UtcNow;
        domain.DeletedBy = _currentUser.UserName;
        await _domains.SaveChangesAsync(cancellationToken);
        await AuditAsync(AuditActions.DeploymentDomainDelete, project, domain.Host, true, cancellationToken);

        var applied = await ApplyProjectAsync(project, cancellationToken);
        return ServiceResult.Success(applied.IsSuccess
            ? "Domain silindi ve yönlendirme güncellendi."
            : "Domain silindi. " + applied.Message);
    }

    public async Task<ServiceResult> ApplyAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult.NotFound(ProjectNotFound);

        if (project.BuildType == DeploymentBuildType.Commands)
            return ServiceResult.Failure("Komutla dağıtılan projede yönlendirme uygulanmaz.");

        if (await IsDeployingAsync(project, cancellationToken))
            return ServiceResult.Failure(DeploymentRunning, ServiceErrorType.Conflict);

        return await ApplyProjectAsync(project, cancellationToken);
    }

    public async Task<ServiceResult> ValidateProjectChangeAsync(
        DeploymentProject project, Guid serverId, DeploymentBuildType buildType, CancellationToken cancellationToken = default)
    {
        var domains = await _domains.ListByProjectAsync(project.Id, cancellationToken);
        if (domains.Count == 0)
            return ServiceResult.Success();

        if (buildType == DeploymentBuildType.DockerCompose)
        {
            var missing = domains.FirstOrDefault(domain => !DomainNames.IsValidServiceName(domain.ServiceName));
            if (missing is not null)
            {
                return ServiceResult.ValidationFailure(
                    nameof(ProjectFormDto.BuildType),
                    $"{missing.Host} domaininde Compose servis adı yok. Docker Compose'a geçmeden önce domainleri düzenleyip servis adını girin veya domainleri silin.");
            }
        }

        if (serverId == project.ServerId)
            return ServiceResult.Success();

        foreach (var domain in domains)
        {
            if (await _domains.HostPathExistsAsync(serverId, domain.Host, domain.Path, domain.Id, cancellationToken))
                return ServiceResult.Failure($"Seçilen sunucuda {domain.Host}{domain.Path} başka bir domaine ait; proje bu sunucuya taşınamaz.", ServiceErrorType.Conflict);
        }

        return ServiceResult.Success();
    }

    public async Task OnProjectServerChangedAsync(DeploymentProject project, CancellationToken cancellationToken = default)
    {
        var domains = await _domains.ListByProjectAsync(project.Id, cancellationToken);
        var moved = domains.Where(domain => domain.ServerId != project.ServerId).ToList();
        if (moved.Count == 0)
            return;

        foreach (var domain in moved)
        {
            domain.ServerId = project.ServerId;
            domain.UpdatedAt = UtcNow;
            domain.UpdatedBy = _currentUser.UserName;
        }

        await _domains.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Proje başka sunucuya taşındı; domain kayıtları güncellendi. ProjectId: {ProjectId}, Count: {Count}", project.Id, moved.Count);
    }

    private async Task<bool> IsDeployingAsync(DeploymentProject project, CancellationToken cancellationToken) =>
        await _projects.GetRunningDeploymentAsync(project.Id, cancellationToken) is not null;

    public async Task OnProjectDeletedAsync(DeploymentProject project, CancellationToken cancellationToken = default, bool detachRouting = true)
    {
        await _domains.SoftDeleteByProjectAsync(project.Id, _currentUser.UserName, UtcNow, cancellationToken);
        await _domains.SaveChangesAsync(cancellationToken);

        if (!detachRouting || project.BuildType == DeploymentBuildType.Commands)
            return;

        try
        {
            var applied = await ApplyProjectAsync(project, cancellationToken);
            if (!applied.IsSuccess)
                _logger.LogInformation("Proje silindikten sonra yönlendirme kaldırılamadı. ProjectId: {ProjectId}, Message: {Message}", project.Id, applied.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Proje silindikten sonra yönlendirme kaldırılamadı. ProjectId: {ProjectId}", project.Id);
        }
    }

    private async Task<ServiceResult> ApplyProjectAsync(DeploymentProject project, CancellationToken cancellationToken)
    {
        var routes = await BuildRoutesAsync(project, cancellationToken);
        if (!routes.IsSuccess)
            return ServiceResult.Failure(routes.Message ?? "Yönlendirme hazırlanamadı.", routes.ErrorType);

        var connection = await _connectionProvider.GetAsync(project.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        PortMappings.TryParse(project.PortMappings, out var ports, out _);
        var plan = new DeploymentPlan
        {
            Slug = project.Slug,
            Source = new GitSource { RepositoryUrl = project.RepositoryUrl },
            Branch = project.Branch,
            DeployPath = project.DeployPath,
            BuildType = project.BuildType,
            ComposeFile = project.ComposeFile ?? "docker-compose.yml",
            DockerfilePath = project.DockerfilePath ?? "Dockerfile",
            PortMappings = ports,
            Environment = project.EncryptedEnvironment is null ? null : string.Empty,
            Routes = routes.Data!,
            JoinServicesNetwork = project.BuildType != DeploymentBuildType.Commands && await _projects.HasServiceLinksAsync(project.Id, cancellationToken),
            DeployTimeout = TimeSpan.FromMinutes(Math.Max(1, _options.DeployTimeoutMinutes))
        };

        return await _provider.ApplyRoutingAsync(
            connection.Data!.Context,
            plan,
            plan.DeployTimeout,
            cancellationToken);
    }

    public Task<ServiceResult<IReadOnlyList<DeploymentRoute>>> GetRoutesAsync(DeploymentProject project, CancellationToken cancellationToken = default) =>
        BuildRoutesAsync(project, cancellationToken);

    private async Task<ServiceResult<IReadOnlyList<DeploymentRoute>>> BuildRoutesAsync(DeploymentProject project, CancellationToken cancellationToken)
    {
        var domains = await _domains.ListByProjectAsync(project.Id, cancellationToken);
        var routes = new List<DeploymentRoute>(domains.Count);
        foreach (var domain in domains)
        {
            string? certificate = null;
            string? key = null;
            if (domain.TlsMode == DeploymentTlsMode.Custom)
            {
                try
                {
                    certificate = domain.EncryptedCertificate is null ? null : _secretProtector.Unprotect(domain.EncryptedCertificate);
                    key = domain.EncryptedPrivateKey is null ? null : _secretProtector.Unprotect(domain.EncryptedPrivateKey);
                }
                catch (CryptographicException ex)
                {
                    _logger.LogError(ex, "Domain sertifikası çözülemedi. DomainId: {DomainId}", domain.Id);
                    return ServiceResult<IReadOnlyList<DeploymentRoute>>.Failure($"{domain.Host} sertifikası çözülemedi. Master key değişmiş olabilir; sertifikayı yeniden girin.");
                }

                if (string.IsNullOrEmpty(certificate) || string.IsNullOrEmpty(key))
                    return ServiceResult<IReadOnlyList<DeploymentRoute>>.Failure($"{domain.Host} için özel sertifika eksik.");
            }

            if (project.BuildType == DeploymentBuildType.DockerCompose && !DomainNames.IsValidServiceName(domain.ServiceName))
                return ServiceResult<IReadOnlyList<DeploymentRoute>>.Failure($"{domain.Host} domaininde Compose servis adı yok; domaini düzenleyip servis adını girin (ör. web).");

            routes.Add(new DeploymentRoute
            {
                RouterName = DomainNames.RouterName(project.Slug, domain.Host, domain.Path),
                Host = domain.Host,
                Path = domain.Path,
                ContainerPort = domain.ContainerPort,
                ServiceName = domain.ServiceName,
                TlsMode = domain.TlsMode,
                CertificatePem = certificate,
                PrivateKeyPem = key
            });
        }

        return ServiceResult<IReadOnlyList<DeploymentRoute>>.Success(routes);
    }

    private ServiceResult ApplySecrets(DeploymentDomain domain, DomainFormDto dto, bool creating)
    {
        if (dto.TlsMode != DeploymentTlsMode.Custom)
        {
            domain.EncryptedCertificate = null;
            domain.EncryptedPrivateKey = null;
            return ServiceResult.Success();
        }

        var hasCertificate = !string.IsNullOrWhiteSpace(dto.CertificatePem);
        var hasKey = !string.IsNullOrWhiteSpace(dto.PrivateKey);
        if (!hasCertificate && !hasKey && !creating)
            return ServiceResult.Success();

        domain.EncryptedCertificate = _secretProtector.Protect(dto.CertificatePem!.Trim());
        domain.EncryptedPrivateKey = _secretProtector.Protect(dto.PrivateKey!.Trim());
        return ServiceResult.Success();
    }

    private static DomainListItemDto ToListItem(DeploymentDomain domain) => new()
    {
        Id = domain.Id,
        Host = domain.Host,
        Path = domain.Path,
        ContainerPort = domain.ContainerPort,
        ServiceName = domain.ServiceName,
        TlsMode = domain.TlsMode,
        HasCertificate = domain.EncryptedPrivateKey is not null
    };

    private Task AuditAsync(string action, DeploymentProject project, string? details, bool isSuccess, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.Project, project.Id.ToString(), project.Name, details, isSuccess), cancellationToken);
}
