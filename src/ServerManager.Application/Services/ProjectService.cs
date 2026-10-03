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
    private readonly IServerRepository _serverRepository;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IDeploymentProvider _provider;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateProjectDto> _createValidator;
    private readonly IValidator<UpdateProjectDto> _updateValidator;
    private readonly DeploymentOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        IDeploymentRepository repository,
        IServerRepository serverRepository,
        IServerConnectionProvider connectionProvider,
        IDeploymentProvider provider,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<CreateProjectDto> createValidator,
        IValidator<UpdateProjectDto> updateValidator,
        IOptions<DeploymentOptions> options,
        TimeProvider timeProvider,
        ILogger<ProjectService> logger)
    {
        _repository = repository;
        _serverRepository = serverRepository;
        _connectionProvider = connectionProvider;
        _provider = provider;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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
        return ServiceResult<ProjectDetailsDto>.Success(project.ToDetailsDto(keys, unreadable, running?.Id));
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
        await AuditAsync(AuditActions.ProjectCreate, project, $"Sunucu: {server.Name} | Depo: {project.RepositoryUrl} ({project.Branch}) | Tür: {project.BuildType}", cancellationToken);
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

        var keepsToken = string.IsNullOrEmpty(dto.AccessToken) && !dto.RemoveAccessToken && project.EncryptedAccessToken is not null;
        if (keepsToken && !GitRepositoryUrls.IsHttps(dto.RepositoryUrl))
            return ServiceResult.ValidationFailure(nameof(dto.RepositoryUrl), "Kayıtlı erişim anahtarı yalnızca https:// adreslerle kullanılabilir; anahtarı kaldırın veya https adres girin.");

        if (await _repository.GetRunningDeploymentAsync(project.Id, cancellationToken) is not null)
            return ServiceResult.Failure("Proje için süren bir deployment var; bitmesini bekleyin veya iptal edin.", ServiceErrorType.Conflict);

        var changes = DescribeChanges(project, dto, server.Name);
        ApplyFormValues(project, dto);

        if (!string.IsNullOrEmpty(dto.AccessToken))
        {
            project.EncryptedAccessToken = _secretProtector.Protect(dto.AccessToken);
            changes.Add("Erişim anahtarı güncellendi");
        }
        else if (dto.RemoveAccessToken && project.EncryptedAccessToken is not null)
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

    public async Task<ServiceResult> DeleteAsync(Guid id, string? confirmationName, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(id, cancellationToken);
        if (project is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (!string.Equals(confirmationName?.Trim(), project.Name, StringComparison.Ordinal))
            return ServiceResult.ValidationFailure("ConfirmationName", "Silme işlemini onaylamak için proje adını birebir yazın.");

        if (await _repository.GetRunningDeploymentAsync(id, cancellationToken) is not null)
            return ServiceResult.Failure("Proje için süren bir deployment var; bitmesini bekleyin veya iptal edin.", ServiceErrorType.Conflict);

        project.IsDeleted = true;
        project.DeletedAt = UtcNow;
        project.DeletedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Deployment projesi silindi (soft delete). ProjectId: {ProjectId}, Name: {ProjectName}", project.Id, project.Name);
        await AuditAsync(AuditActions.ProjectDelete, project, $"Sunucudaki {project.DeployPath} klasörü ve container'lar silinmedi.", cancellationToken);
        return ServiceResult.Success("Proje silindi. Sunucudaki dosyalar ve çalışan uygulama olduğu gibi bırakıldı.");
    }

    public async Task<ServiceResult<GitBranchListDto>> ListBranchesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(id, cancellationToken);
        if (project is null)
            return ServiceResult<GitBranchListDto>.NotFound(NotFoundMessage);

        var connection = await _connectionProvider.GetAsync(project.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<GitBranchListDto>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        string? token;
        try
        {
            token = project.EncryptedAccessToken is null ? null : _secretProtector.Unprotect(project.EncryptedAccessToken);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Git erişim anahtarı çözülemedi. ProjectId: {ProjectId}", project.Id);
            return ServiceResult<GitBranchListDto>.Failure("Erişim anahtarı çözülemedi. Master key değişmiş olabilir; anahtarı yeniden girin.");
        }

        var source = new GitSource
        {
            RepositoryUrl = project.RepositoryUrl,
            Username = GitRepositoryUrls.TokenUsername(project.GitProvider, project.GitUsername),
            AccessToken = token
        };
        var branches = await _provider.ListBranchesAsync(connection.Data!.Context, source, TimeSpan.FromSeconds(Math.Clamp(_options.GitTimeoutSeconds, 15, 120)), cancellationToken);
        if (!branches.IsSuccess)
            return ServiceResult<GitBranchListDto>.Failure(branches.Message ?? "Depoya erişilemedi.", branches.ErrorType);

        return ServiceResult<GitBranchListDto>.Success(new GitBranchListDto
        {
            Branches = branches.Data!,
            ConfiguredBranch = project.Branch,
            ConfiguredBranchExists = branches.Data!.Contains(project.Branch, StringComparer.Ordinal)
        });
    }

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
