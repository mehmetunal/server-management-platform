using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class ProjectService : IProjectService
{
    private const string NotFoundMessage = "Proje bulunamadı.";
    private const int MaxSlugAttempts = 50;

    private readonly IDeploymentRepository _repository;
    private readonly IDeploymentDomainService _domains;
    private readonly IServerRepository _serverRepository;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IDeploymentProvider _provider;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateProjectDto> _createValidator;
    private readonly IValidator<UpdateProjectDto> _updateValidator;
    private readonly IValidator<RemoteBranchQueryDto> _remoteBranchValidator;
    private readonly IGitIntegrationRegistry _gitIntegrations;
    private readonly DeploymentOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        IDeploymentRepository repository,
        IDeploymentDomainService domains,
        IServerRepository serverRepository,
        IServerConnectionProvider connectionProvider,
        IDeploymentProvider provider,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<CreateProjectDto> createValidator,
        IValidator<UpdateProjectDto> updateValidator,
        IValidator<RemoteBranchQueryDto> remoteBranchValidator,
        IGitIntegrationRegistry gitIntegrations,
        IOptions<DeploymentOptions> options,
        TimeProvider timeProvider,
        ILogger<ProjectService> logger)
    {
        _repository = repository;
        _domains = domains;
        _serverRepository = serverRepository;
        _connectionProvider = connectionProvider;
        _provider = provider;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _remoteBranchValidator = remoteBranchValidator;
        _gitIntegrations = gitIntegrations;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<ProjectListItemDto>> SearchAsync(ProjectFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = await _repository.SearchProjectsAsync(filter, cancellationToken);
        var latest = await _repository.GetLatestDeploymentsAsync(page.Items.Select(p => p.Id).ToList(), cancellationToken);
        return page.Map(p => p.ToListItemDto(latest.GetValueOrDefault(p.Id)));
    }

    public async Task<IReadOnlyList<ProjectListItemDto>> GetByServerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var projects = await _repository.GetProjectsByServerAsync(serverId, cancellationToken);
        var latest = await _repository.GetLatestDeploymentsAsync(projects.Select(p => p.Id).ToList(), cancellationToken);
        return projects.Select(p => p.ToListItemDto(latest.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<ServiceResult<ProjectDetailsDto>> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(id, cancellationToken);
        if (project is null)
            return ServiceResult<ProjectDetailsDto>.NotFound(NotFoundMessage);

        var (keys, unreadable) = ReadEnvironmentKeys(project);
        var running = await _repository.GetRunningDeploymentAsync(id, cancellationToken);
        var integrationName = project.GitIntegration is null ? null : _gitIntegrations.Find(project.GitIntegration)?.DisplayName;
        return ServiceResult<ProjectDetailsDto>.Success(project.ToDetailsDto(keys, unreadable, running?.Id, integrationName));
    }

    public async Task<ServiceResult<UpdateProjectDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(id, cancellationToken);
        if (project is null)
            return ServiceResult<UpdateProjectDto>.NotFound(NotFoundMessage);

        var (keys, _) = ReadEnvironmentKeys(project);
        return ServiceResult<UpdateProjectDto>.Success(project.ToUpdateDto(keys));
    }

    public async Task<IReadOnlyList<ServerOptionDto>> GetServerOptionsAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.FindAsync(_ => true, cancellationToken);
        return servers
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new ServerOptionDto(s.Id, s.Name, s.IpAddress))
            .ToList();
    }

    public async Task<ServiceResult<Guid>> CreateAsync(CreateProjectDto dto, CancellationToken cancellationToken = default)
    {
        Trim(dto);
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var server = await _serverRepository.GetByIdAsync(dto.ServerId, cancellationToken);
        if (server is null)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.ServerId), "Seçilen sunucu bulunamadı.");

        if (await _repository.ProjectNameExistsAsync(dto.Name, null, cancellationToken))
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Name), "Bu isimde bir proje zaten kayıtlı.");

        var integrationError = await ApplyIntegrationAsync(dto, cancellationToken);
        if (integrationError is not null)
            return ServiceResult<Guid>.ValidationFailure([integrationError]);

        var slug = await GenerateSlugAsync(dto.Name, cancellationToken);
        if (slug is null)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Name), "Bu ad için benzersiz bir kısa ad üretilemedi; farklı bir ad deneyin.");

        var project = new DeploymentProject { Slug = slug, CreatedBy = _currentUser.UserName, CreatedAt = UtcNow };
        ApplyFormValues(project, dto);
        if (!string.IsNullOrEmpty(dto.AccessToken))
            project.EncryptedAccessToken = _secretProtector.Protect(dto.AccessToken);
        if (!string.IsNullOrWhiteSpace(dto.Environment))
            project.EncryptedEnvironment = _secretProtector.Protect(EnvironmentFile.Normalize(dto.Environment));

        await _repository.AddProjectAsync(project, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deployment projesi eklendi. ProjectId: {ProjectId}, Name: {ProjectName}", project.Id, project.Name);
        await AuditAsync(AuditActions.ProjectCreate, project, $"Sunucu: {server.Name} | Depo: {DescribeSource(project)} ({project.Branch}) | Tür: {project.BuildType}", cancellationToken);
        return ServiceResult<Guid>.Success(project.Id, "Proje eklendi.");
    }

    public async Task<ServiceResult> UpdateAsync(UpdateProjectDto dto, CancellationToken cancellationToken = default)
    {
        Trim(dto);
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var project = await _repository.GetProjectAsync(dto.Id, cancellationToken);
        if (project is null)
            return ServiceResult.NotFound(NotFoundMessage);

        var server = await _serverRepository.GetByIdAsync(dto.ServerId, cancellationToken);
        if (server is null)
            return ServiceResult.ValidationFailure(nameof(dto.ServerId), "Seçilen sunucu bulunamadı.");

        if (await _repository.ProjectNameExistsAsync(dto.Name, project.Id, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), "Bu isimde bir proje zaten kayıtlı.");

        var integrationError = await ApplyIntegrationAsync(dto, cancellationToken);
        if (integrationError is not null)
            return ServiceResult.ValidationFailure([integrationError]);

        var keepsToken = !dto.UsesIntegration && string.IsNullOrEmpty(dto.AccessToken) && !dto.RemoveAccessToken && project.EncryptedAccessToken is not null;
        if (keepsToken && !GitRepositoryUrls.IsHttps(dto.RepositoryUrl))
            return ServiceResult.ValidationFailure(nameof(dto.RepositoryUrl), "Kayıtlı erişim anahtarı yalnızca https:// adreslerle kullanılabilir; anahtarı kaldırın veya https adres girin.");
        if (keepsToken && !GitRepositoryUrls.HaveSameHost(project.RepositoryUrl, dto.RepositoryUrl))
            return ServiceResult.ValidationFailure(nameof(dto.AccessToken), "Depo adresi başka bir sunucuya çevrildi; kayıtlı erişim anahtarı yeni adrese gönderilmez. Anahtarı yeniden girin veya kaldırın.");

        if (await _repository.GetRunningDeploymentAsync(project.Id, cancellationToken) is not null)
            return ServiceResult.Failure("Proje için süren bir deployment var; bitmesini bekleyin veya iptal edin.", ServiceErrorType.Conflict);

        var changes = DescribeChanges(project, dto, server.Name);
        ApplyFormValues(project, dto);

        if (!string.IsNullOrEmpty(dto.AccessToken))
        {
            project.EncryptedAccessToken = _secretProtector.Protect(dto.AccessToken);
            changes.Add("Erişim anahtarı güncellendi");
        }
        else if ((dto.RemoveAccessToken || dto.UsesIntegration) && project.EncryptedAccessToken is not null)
        {
            project.EncryptedAccessToken = null;
            changes.Add("Erişim anahtarı kaldırıldı");
        }

        if (!string.IsNullOrWhiteSpace(dto.Environment))
        {
            project.EncryptedEnvironment = _secretProtector.Protect(EnvironmentFile.Normalize(dto.Environment));
            changes.Add("Ortam değişkenleri güncellendi");
        }
        else if (dto.RemoveEnvironment && project.EncryptedEnvironment is not null)
        {
            project.EncryptedEnvironment = null;
            changes.Add("Ortam değişkenleri kaldırıldı");
        }

        project.UpdatedAt = UtcNow;
        project.UpdatedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.ProjectUpdate, project, changes.Count > 0 ? string.Join(" | ", changes) : "Değişiklik yok", cancellationToken);
        return ServiceResult.Success("Proje güncellendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, string? confirmationName, bool hardDelete, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(id, cancellationToken);
        if (project is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (!string.Equals(confirmationName?.Trim(), project.Name, StringComparison.Ordinal))
            return ServiceResult.ValidationFailure("ConfirmationName", "Silme işlemini onaylamak için proje adını birebir yazın.");

        if (await _repository.GetRunningDeploymentAsync(id, cancellationToken) is not null)
            return ServiceResult.Failure("Proje için süren bir deployment var; bitmesini bekleyin veya iptal edin.", ServiceErrorType.Conflict);

        if (hardDelete)
        {
            var removed = await RemoveFromServerAsync(project, cancellationToken);
            if (!removed.IsSuccess)
                return removed;
        }

        project.IsDeleted = true;
        project.DeletedAt = UtcNow;
        project.DeletedBy = _currentUser.UserName;
        await _domains.OnProjectDeletedAsync(project, cancellationToken, detachRouting: !hardDelete);

        var detail = hardDelete
            ? $"Sunucudaki {project.DeployPath} klasörü, container, imaj, volume ve domain yönlendirmesi silindi."
            : $"Sunucudaki {project.DeployPath} klasörü ve container'lar silinmedi.";
        _logger.LogWarning("Deployment projesi silindi. ProjectId: {ProjectId}, Name: {ProjectName}, HardDelete: {HardDelete}", project.Id, project.Name, hardDelete);
        await AuditAsync(AuditActions.ProjectDelete, project, detail, cancellationToken);
        return ServiceResult.Success(hardDelete
            ? "Proje silindi. Sunucudaki klasör, container, imaj ve domain yönlendirmesi kaldırıldı."
            : "Proje silindi. Sunucudaki dosyalar ve çalışan uygulama olduğu gibi bırakıldı.");
    }

    private async Task<ServiceResult> RemoveFromServerAsync(DeploymentProject project, CancellationToken cancellationToken)
    {
        if (!DeployPaths.TryValidate(project.DeployPath, out var pathError))
            return ServiceResult.Failure(pathError ?? "Deploy klasörü silinemez.");

        var connection = await _connectionProvider.GetAsync(project.ServerId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var plan = new DeploymentPlan
        {
            Slug = project.Slug,
            Source = new GitSource { RepositoryUrl = project.RepositoryUrl },
            Branch = project.Branch,
            DeployPath = project.DeployPath,
            BuildType = project.BuildType,
            ComposeFile = project.ComposeFile ?? "docker-compose.yml",
            DockerfilePath = project.DockerfilePath ?? "Dockerfile"
        };
        return await _provider.RemoveDeploymentAsync(
            connection.Data.Context,
            plan,
            TimeSpan.FromMinutes(Math.Max(1, _options.DeployTimeoutMinutes)),
            cancellationToken);
    }

    public async Task<ServiceResult<GitBranchListDto>> ListBranchesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(id, cancellationToken);
        if (project is null)
            return ServiceResult<GitBranchListDto>.NotFound(NotFoundMessage);

        var branches = project.GitIntegration is null
            ? await ListBranchesOverSshAsync(project, cancellationToken)
            : await ListIntegrationBranchesAsync(project.GitIntegration, project.GitSourceId, project.GitRepository, cancellationToken);
        if (!branches.IsSuccess)
            return ServiceResult<GitBranchListDto>.Failure(branches.Message ?? "Depoya erişilemedi.", branches.ErrorType);

        return ServiceResult<GitBranchListDto>.Success(new GitBranchListDto
        {
            Branches = branches.Data!,
            ConfiguredBranch = project.Branch,
            ConfiguredBranchExists = branches.Data!.Contains(project.Branch, StringComparer.Ordinal)
        });
    }

    public async Task<GitSourceListDto> GetGitSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = new List<GitSourceOptionDto>();
        var warnings = new List<string>();
        foreach (var integration in _gitIntegrations.GetEnabled())
        {
            var result = await CallIntegrationAsync(integration, i => i.ListSourcesAsync(cancellationToken));
            if (!result.IsSuccess)
            {
                warnings.Add($"{integration.DisplayName}: {result.Message}");
                continue;
            }

            sources.AddRange(result.Data!.Select(source =>
                new GitSourceOptionDto(GitSourceKeys.Format(integration.SystemName, source.Id), source.Name, integration.DisplayName)));
        }

        return new GitSourceListDto { Sources = sources, Warnings = warnings };
    }

    public async Task<ServiceResult<IReadOnlyList<GitRepositoryDto>>> ListGitRepositoriesAsync(string? sourceKey, CancellationToken cancellationToken = default)
    {
        if (!GitSourceKeys.TryParse(sourceKey, out var systemName, out var sourceId))
            return ServiceResult<IReadOnlyList<GitRepositoryDto>>.ValidationFailure(nameof(ProjectFormDto.GitSource), "Geçerli bir Git bağlantısı seçin.");

        var integration = _gitIntegrations.Find(systemName);
        if (integration is null)
            return ServiceResult<IReadOnlyList<GitRepositoryDto>>.Failure(IntegrationDisabledMessage(systemName));

        return await CallIntegrationAsync(integration, i => i.ListRepositoriesAsync(sourceId, cancellationToken));
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> ListGitBranchesAsync(string? sourceKey, string? repository, CancellationToken cancellationToken = default)
    {
        if (!GitSourceKeys.TryParse(sourceKey, out var systemName, out var sourceId))
            return ServiceResult<IReadOnlyList<string>>.ValidationFailure(nameof(ProjectFormDto.GitSource), "Geçerli bir Git bağlantısı seçin.");

        return await ListIntegrationBranchesAsync(systemName, sourceId, repository, cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> ListRemoteBranchesAsync(RemoteBranchQueryDto dto, CancellationToken cancellationToken = default)
    {
        dto.RepositoryUrl = dto.RepositoryUrl?.Trim() ?? string.Empty;
        dto.GitUsername = TextHelper.NullIfEmpty(dto.GitUsername);
        dto.AccessToken = TextHelper.NullIfEmpty(dto.AccessToken);
        var validation = await _remoteBranchValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<IReadOnlyList<string>>.ValidationFailure(validation);

        var token = dto.AccessToken;
        if (token is null && !dto.RemoveAccessToken && dto.ProjectId is { } projectId)
        {
            var project = await _repository.GetProjectAsync(projectId, cancellationToken);
            if (project?.EncryptedAccessToken is not null && GitRepositoryUrls.HaveSameHost(project.RepositoryUrl, dto.RepositoryUrl))
            {
                var stored = UnprotectToken(project);
                if (!stored.IsSuccess)
                    return ServiceResult<IReadOnlyList<string>>.Failure(stored.Message!);

                token = stored.Data;
            }
        }

        var source = new GitSource
        {
            RepositoryUrl = dto.RepositoryUrl,
            Username = GitRepositoryUrls.TokenUsername(dto.GitProvider, dto.GitUsername),
            AccessToken = token
        };
        return await ListBranchesOverSshAsync(dto.ServerId, source, cancellationToken);
    }

    private async Task<ServiceResult<IReadOnlyList<string>>> ListIntegrationBranchesAsync(string systemName, string? sourceId, string? repository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sourceId) || !GitSourceKeys.IsValidRepository(repository))
            return ServiceResult<IReadOnlyList<string>>.ValidationFailure(nameof(ProjectFormDto.GitRepository), "Bağlantıdaki depolardan birini seçin.");

        var integration = _gitIntegrations.Find(systemName);
        if (integration is null)
            return ServiceResult<IReadOnlyList<string>>.Failure(IntegrationDisabledMessage(systemName));

        return await CallIntegrationAsync(integration, i => i.ListBranchesAsync(sourceId, repository!, cancellationToken));
    }

    private async Task<ServiceResult<IReadOnlyList<string>>> ListBranchesOverSshAsync(DeploymentProject project, CancellationToken cancellationToken)
    {
        var token = UnprotectToken(project);
        if (!token.IsSuccess)
            return ServiceResult<IReadOnlyList<string>>.Failure(token.Message!);

        var source = new GitSource
        {
            RepositoryUrl = project.RepositoryUrl,
            Username = GitRepositoryUrls.TokenUsername(project.GitProvider, project.GitUsername),
            AccessToken = token.Data
        };
        return await ListBranchesOverSshAsync(project.ServerId, source, cancellationToken);
    }

    private async Task<ServiceResult<IReadOnlyList<string>>> ListBranchesOverSshAsync(Guid serverId, GitSource source, CancellationToken cancellationToken)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<IReadOnlyList<string>>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await _provider.ListBranchesAsync(connection.Data!.Context, source, TimeSpan.FromSeconds(Math.Clamp(_options.GitTimeoutSeconds, 15, 120)), cancellationToken);
    }

    private ServiceResult<string?> UnprotectToken(DeploymentProject project)
    {
        try
        {
            return ServiceResult<string?>.Success(project.EncryptedAccessToken is null ? null : _secretProtector.Unprotect(project.EncryptedAccessToken));
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Git erişim anahtarı çözülemedi. ProjectId: {ProjectId}", project.Id);
            return ServiceResult<string?>.Failure("Erişim anahtarı çözülemedi. Master key değişmiş olabilir; anahtarı yeniden girin.");
        }
    }

    /// <summary>
    /// Entegrasyonla bağlanan depoda adres, sağlayıcı ve kimlik bilgisi entegrasyondan gelir; formdaki değerler yok sayılır.
    /// Seçilen deponun gerçekten o bağlantıdan erişilebilir olduğu burada doğrulanır.
    /// </summary>
    private async Task<ServiceError?> ApplyIntegrationAsync(ProjectFormDto dto, CancellationToken cancellationToken)
    {
        if (!dto.UsesIntegration)
        {
            dto.GitRepository = null;
            return null;
        }

        GitSourceKeys.TryParse(dto.GitSource, out var systemName, out var sourceId);
        var integration = _gitIntegrations.Find(systemName);
        if (integration is null)
            return new ServiceError(nameof(dto.GitSource), IntegrationDisabledMessage(systemName));

        var repository = await CallIntegrationAsync(integration, i => i.GetRepositoryAsync(sourceId, dto.GitRepository!, cancellationToken));
        if (!repository.IsSuccess)
            return new ServiceError(nameof(dto.GitRepository), repository.Message ?? "Depoya erişilemedi.");

        if (!GitRepositoryUrls.TryValidate(repository.Data!.CloneUrl, out var error))
            return new ServiceError(nameof(dto.GitRepository), $"Deponun adresi kullanılamıyor: {error}");

        dto.GitRepository = repository.Data.FullName;
        dto.RepositoryUrl = repository.Data.CloneUrl;
        dto.GitProvider = integration.Provider;
        dto.GitUsername = null;
        dto.AccessToken = null;
        return null;
    }

    private async Task<ServiceResult<T>> CallIntegrationAsync<T>(IGitIntegration integration, Func<IGitIntegration, Task<ServiceResult<T>>> call)
    {
        try
        {
            return await call(integration);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Git entegrasyonu beklenmeyen hata verdi. Integration: {Integration}", integration.SystemName);
            return ServiceResult<T>.Failure($"{integration.DisplayName} beklenmeyen bir hata verdi; ayrıntılar uygulama loglarında.");
        }
    }

    private static string IntegrationDisabledMessage(string systemName) =>
        $"Git entegrasyonu ({systemName}) kurulu veya etkin değil; Eklentiler sayfasından etkinleştirin ya da depo adresi kullanın.";

    private static string DescribeSource(DeploymentProject project) =>
        project.GitIntegration is null ? project.RepositoryUrl : $"{project.GitRepository} ({project.GitIntegration})";

    private async Task<string?> GenerateSlugAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = DeploymentNames.Slugify(name);
        for (var attempt = 1; attempt <= MaxSlugAttempts; attempt++)
        {
            var candidate = attempt == 1 ? baseSlug : DeploymentNames.WithSuffix(baseSlug, attempt);
            if (!await _repository.SlugExistsAsync(candidate, cancellationToken))
                return candidate;
        }

        return null;
    }

    private (IReadOnlyList<string> Keys, bool Unreadable) ReadEnvironmentKeys(DeploymentProject project)
    {
        if (project.EncryptedEnvironment is null)
            return ([], false);

        try
        {
            EnvironmentFile.TryParse(_secretProtector.Unprotect(project.EncryptedEnvironment), out var keys, out _);
            return (keys, false);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Proje ortam değişkenleri çözülemedi. ProjectId: {ProjectId}", project.Id);
            return ([], true);
        }
    }

    private static void Trim(ProjectFormDto dto)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.GitSource = TextHelper.NullIfEmpty(dto.GitSource);
        dto.GitRepository = TextHelper.NullIfEmpty(dto.GitRepository);
        dto.RepositoryUrl = dto.RepositoryUrl?.Trim() ?? string.Empty;
        dto.Branch = dto.Branch?.Trim() ?? string.Empty;
        dto.DeployPath = dto.DeployPath?.Trim() ?? string.Empty;
        dto.ComposeFile = TextHelper.NullIfEmpty(dto.ComposeFile);
        dto.DockerfilePath = TextHelper.NullIfEmpty(dto.DockerfilePath);
        dto.GitUsername = TextHelper.NullIfEmpty(dto.GitUsername);
        dto.AccessToken = TextHelper.NullIfEmpty(dto.AccessToken);
        dto.BuildCommand = TextHelper.NullIfEmpty(dto.BuildCommand);
        dto.DeployCommand = TextHelper.NullIfEmpty(dto.DeployCommand);
        dto.PortMappings = TextHelper.NullIfEmpty(dto.PortMappings);
    }

    private static void ApplyFormValues(DeploymentProject project, ProjectFormDto dto)
    {
        project.ServerId = dto.ServerId;
        project.Name = dto.Name;
        project.Description = TextHelper.NullIfEmpty(dto.Description);
        GitSourceKeys.TryParse(dto.GitSource, out var integration, out var sourceId);
        project.GitIntegration = dto.UsesIntegration ? integration : null;
        project.GitSourceId = dto.UsesIntegration ? sourceId : null;
        project.GitRepository = dto.UsesIntegration ? dto.GitRepository : null;
        project.GitProvider = dto.GitProvider;
        project.RepositoryUrl = dto.RepositoryUrl;
        project.Branch = dto.Branch;
        project.GitUsername = dto.GitUsername;
        project.DeployPath = dto.DeployPath;
        project.BuildType = dto.BuildType;
        project.ComposeFile = dto.BuildType == DeploymentBuildType.DockerCompose ? dto.ComposeFile : project.ComposeFile;
        project.DockerfilePath = dto.BuildType == DeploymentBuildType.Dockerfile ? dto.DockerfilePath : project.DockerfilePath;
        project.PortMappings = PortMappings.TryParse(dto.PortMappings, out var mappings, out _) && mappings.Count > 0 ? string.Join(", ", mappings) : null;
        project.BuildCommand = dto.BuildCommand;
        project.DeployCommand = dto.DeployCommand;
        project.UseSudoForCommands = dto.UseSudoForCommands;
    }

    private static List<string> DescribeChanges(DeploymentProject project, ProjectFormDto dto, string serverName)
    {
        var changes = new List<string>();
        if (project.ServerId != dto.ServerId)
            changes.Add($"Sunucu: {serverName}");
        if (!string.Equals(project.RepositoryUrl, dto.RepositoryUrl, StringComparison.Ordinal))
            changes.Add($"Depo: {dto.RepositoryUrl}");
        if (!string.Equals(project.GitSourceId, GitSourceKeys.TryParse(dto.GitSource, out _, out var sourceId) ? sourceId : null, StringComparison.Ordinal))
            changes.Add(dto.UsesIntegration ? $"Git bağlantısı: {dto.GitSource}" : "Git bağlantısı kaldırıldı; depo adresi kullanılıyor");
        if (!string.Equals(project.Branch, dto.Branch, StringComparison.Ordinal))
            changes.Add($"Dal: {dto.Branch}");
        if (!string.Equals(project.DeployPath, dto.DeployPath, StringComparison.Ordinal))
            changes.Add($"Klasör: {dto.DeployPath}");
        if (project.BuildType != dto.BuildType)
            changes.Add($"Tür: {dto.BuildType}");
        if (!string.Equals(project.BuildCommand, dto.BuildCommand, StringComparison.Ordinal))
            changes.Add("Build komutu değişti");
        if (!string.Equals(project.DeployCommand, dto.DeployCommand, StringComparison.Ordinal))
            changes.Add("Deploy komutu değişti");
        if (project.UseSudoForCommands != dto.UseSudoForCommands)
            changes.Add(dto.UseSudoForCommands ? "Komutlar sudo ile çalışacak" : "Komutlar sudo olmadan çalışacak");
        return changes;
    }

    private Task AuditAsync(string action, DeploymentProject project, string? details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.Project,
            project.Id.ToString(),
            project.Name,
            details), cancellationToken);
}
