using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Domain;
using ServerManager.Plugin.Git.GitHub.DTOs;

namespace ServerManager.Plugin.Git.GitHub.Services;

public class GitHubAppService : IGitHubAppService
{
    private const string StateCachePrefix = "github:manifest-state:";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromHours(1);

    private readonly IGitHubAppRepository _repository;
    private readonly IGitHubApiClient _apiClient;
    private readonly IGitHubAppGateway _gateway;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;
    private readonly IValidator<GitHubManifestRequestDto> _manifestValidator;
    private readonly IValidator<GitHubManualAppDto> _manualValidator;
    private readonly GitHubOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GitHubAppService> _logger;

    public GitHubAppService(
        IGitHubAppRepository repository,
        IGitHubApiClient apiClient,
        IGitHubAppGateway gateway,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IMemoryCache cache,
        IValidator<GitHubManifestRequestDto> manifestValidator,
        IValidator<GitHubManualAppDto> manualValidator,
        IOptions<GitHubOptions> options,
        TimeProvider timeProvider,
        ILogger<GitHubAppService> logger)
    {
        _repository = repository;
        _apiClient = apiClient;
        _gateway = gateway;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _cache = cache;
        _manifestValidator = manifestValidator;
        _manualValidator = manualValidator;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GitHubAppDto>> GetAppsAsync(CancellationToken cancellationToken = default)
    {
        var apps = await _repository.ListAsync(cancellationToken);
        var result = new List<GitHubAppDto>(apps.Count);
        foreach (var app in apps)
        {
            var installations = await _gateway.ListInstallationsAsync(app, useCache: false, cancellationToken);
            var projects = await _repository.GetProjectNamesAsync(app.Id, cancellationToken);
            result.Add(new GitHubAppDto
            {
                Id = app.Id,
                Name = app.Name,
                AppId = app.AppId,
                Slug = app.Slug,
                OwnerLogin = app.OwnerLogin,
                HtmlUrl = app.HtmlUrl,
                InstallUrl = GitHubUrls.InstallUrl(_options.WebUrl, app.Slug),
                CreatedAt = app.CreatedAt,
                CreatedBy = app.CreatedBy,
                Installations = installations.IsSuccess ? installations.Data!.Select(ToDto).ToList() : [],
                InstallationsError = installations.IsSuccess ? null : FailureText(installations),
                ProjectNames = projects
            });
        }

        return result;
    }

    public async Task<ServiceResult<GitHubManifestStartDto>> StartManifestAsync(GitHubManifestRequestDto dto, string panelBaseUrl, CancellationToken cancellationToken = default)
    {
        var validation = await _manifestValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<GitHubManifestStartDto>.ValidationFailure(validation);

        if (!Uri.TryCreate(panelBaseUrl, UriKind.Absolute, out _))
            return ServiceResult<GitHubManifestStartDto>.Failure("Panel adresi belirlenemedi; GitHub:PublicBaseUrl ayarını doldurun.");

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _cache.Set(StateCachePrefix + state, _currentUser.UserId ?? string.Empty, StateLifetime);

        return ServiceResult<GitHubManifestStartDto>.Success(new GitHubManifestStartDto(
            GitHubUrls.ManifestPostUrl(_options.WebUrl, dto.Organization, state),
            GitHubManifest.Build(dto.Name, panelBaseUrl)));
    }

    public async Task<ServiceResult<string>> CompleteManifestAsync(string? code, string? state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state) || code.Length > 200 || state.Length > 100)
            return ServiceResult<string>.Failure("GitHub dönüşünde kod veya durum bilgisi eksik.");

        var stateKey = StateCachePrefix + state;
        if (!_cache.TryGetValue(stateKey, out string? owner) || !string.Equals(owner, _currentUser.UserId ?? string.Empty, StringComparison.Ordinal))
            return ServiceResult<string>.Failure("GitHub dönüşü doğrulanamadı (süresi dolmuş veya başka bir oturumdan başlatılmış); uygulamayı yeniden oluşturun.", ServiceErrorType.Forbidden);

        _cache.Remove(stateKey);

        var conversion = await _apiClient.ConvertManifestAsync(code, cancellationToken);
        if (!conversion.IsSuccess)
            return ServiceResult<string>.Failure(FailureText(conversion), conversion.ErrorType);

        var data = conversion.Data!;
        if (await _repository.ExistsByAppIdAsync(data.Id, cancellationToken))
            return ServiceResult<string>.Failure("Bu GitHub App zaten ekli.", ServiceErrorType.Conflict);

        var app = new GitHubApp
        {
            Name = data.Name,
            AppId = data.Id,
            Slug = data.Slug,
            OwnerLogin = data.OwnerLogin,
            HtmlUrl = data.HtmlUrl,
            ClientId = data.ClientId,
            EncryptedClientSecret = ProtectOptional(data.ClientSecret),
            EncryptedPrivateKey = _secretProtector.Protect(data.Pem),
            EncryptedWebhookSecret = ProtectOptional(data.WebhookSecret),
            CreatedAt = UtcNow,
            CreatedBy = _currentUser.UserName
        };

        await SaveAsync(app, "GitHub'da manifest ile oluşturuldu", cancellationToken);
        return ServiceResult<string>.Success(GitHubUrls.InstallUrl(_options.WebUrl, app.Slug), $"{app.Name} oluşturuldu. Şimdi uygulamayı hesabınıza veya kurumunuza kurun.");
    }

    public async Task<ServiceResult<string>> AddManualAsync(GitHubManualAppDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _manualValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<string>.ValidationFailure(validation);

        if (await _repository.ExistsByAppIdAsync(dto.AppId, cancellationToken))
            return ServiceResult<string>.ValidationFailure(nameof(GitHubManualAppDto.AppId), "Bu GitHub App zaten ekli.");

        var privateKey = dto.PrivateKey.Trim();
        var info = await _gateway.VerifyAsync(dto.AppId, privateKey, cancellationToken);
        if (!info.IsSuccess)
            return ServiceResult<string>.Failure(FailureText(info), info.ErrorType == ServiceErrorType.NotFound ? ServiceErrorType.Failure : info.ErrorType);

        if (info.Data!.Id != dto.AppId)
            return ServiceResult<string>.ValidationFailure(nameof(GitHubManualAppDto.AppId), "Özel anahtar başka bir uygulamaya ait.");

        var app = new GitHubApp
        {
            Name = info.Data.Name,
            AppId = info.Data.Id,
            Slug = info.Data.Slug,
            OwnerLogin = info.Data.OwnerLogin,
            HtmlUrl = info.Data.HtmlUrl,
            EncryptedPrivateKey = _secretProtector.Protect(privateKey),
            CreatedAt = UtcNow,
            CreatedBy = _currentUser.UserName
        };

        await SaveAsync(app, "Uygulama kimliği ve özel anahtarla eklendi", cancellationToken);
        return ServiceResult<string>.Success(GitHubUrls.InstallUrl(_options.WebUrl, app.Slug), $"{app.Name} eklendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var app = await _repository.GetAsync(id, cancellationToken);
        if (app is null)
            return ServiceResult.NotFound("GitHub App bulunamadı.");

        var projects = await _repository.GetProjectNamesAsync(app.Id, cancellationToken);
        if (projects.Count > 0)
            return ServiceResult.Failure(
                $"Bu uygulamayı kullanan projeler var: {string.Join(", ", projects.Take(5))}{(projects.Count > 5 ? "…" : string.Empty)}. Önce projelerin Git kaynağını değiştirin.",
                ServiceErrorType.Conflict);

        app.IsDeleted = true;
        app.DeletedAt = UtcNow;
        app.DeletedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);
        _gateway.Invalidate(app.Id);

        await _auditLogService.LogAsync(new AuditEntry(
            GitHubAuditActions.AppDelete,
            GitHubPlugin.AuditEntityType,
            app.Id.ToString(),
            app.Name,
            $"GitHub App kimliği: {app.AppId}. Uygulama GitHub'da silinmedi; gerekirse GitHub ayarlarından kaldırın."), cancellationToken);

        return ServiceResult.Success($"{app.Name} panelden kaldırıldı. GitHub'daki uygulamayı ayrıca silebilirsiniz.");
    }

    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var app in await _repository.ListAsync(cancellationToken))
            _gateway.Invalidate(app.Id);
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private async Task SaveAsync(GitHubApp app, string how, CancellationToken cancellationToken)
    {
        await _repository.AddAsync(app, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("GitHub App eklendi. Uygulama: {AppId} ({Slug})", app.AppId, app.Slug);

        await _auditLogService.LogAsync(new AuditEntry(
            GitHubAuditActions.AppCreate,
            GitHubPlugin.AuditEntityType,
            app.Id.ToString(),
            app.Name,
            $"{how}. GitHub App kimliği: {app.AppId}, sahibi: {app.OwnerLogin ?? "-"}"), cancellationToken);
    }

    private string? ProtectOptional(string? value) =>
        string.IsNullOrEmpty(value) ? null : _secretProtector.Protect(value);

    private static GitHubInstallationDto ToDto(GitHubInstallationInfo installation) => new()
    {
        Id = installation.Id,
        Account = installation.AccountLogin,
        IsOrganization = string.Equals(installation.AccountType, "Organization", StringComparison.OrdinalIgnoreCase),
        AllRepositories = string.Equals(installation.RepositorySelection, "all", StringComparison.OrdinalIgnoreCase),
        IsSuspended = installation.IsSuspended,
        HtmlUrl = installation.HtmlUrl
    };

    private static string FailureText(ServiceResult result) =>
        result.Errors.FirstOrDefault()?.Message ?? result.Message ?? "GitHub isteği başarısız.";
}
