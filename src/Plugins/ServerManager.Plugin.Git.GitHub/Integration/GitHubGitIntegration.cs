using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Domain.Enums;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Domain;
using ServerManager.Plugin.Git.GitHub.DTOs;
using ServerManager.Plugin.Git.GitHub.Services;

namespace ServerManager.Plugin.Git.GitHub.Integration;

public sealed class GitHubGitIntegration : IGitIntegration
{
    /// <summary>GitHub, kurulum anahtarıyla https üzerinden çekmede bu kullanıcı adını bekler.</summary>
    public const string TokenUsername = "x-access-token";

    private const string SourceNotFoundMessage = "GitHub bağlantısı bulunamadı; uygulama kaldırılmış olabilir.";

    private readonly IGitHubAppRepository _repository;
    private readonly IGitHubAppGateway _gateway;
    private readonly ILogger<GitHubGitIntegration> _logger;

    public GitHubGitIntegration(IGitHubAppRepository repository, IGitHubAppGateway gateway, ILogger<GitHubGitIntegration> logger)
    {
        _repository = repository;
        _gateway = gateway;
        _logger = logger;
    }

    public string SystemName => GitHubPlugin.SystemName;

    public string DisplayName => GitHubPlugin.DisplayName;

    public GitProvider Provider => GitProvider.GitHub;

    public async Task<ServiceResult<IReadOnlyList<GitSourceDto>>> ListSourcesAsync(CancellationToken cancellationToken = default)
    {
        var apps = await _repository.ListAsync(cancellationToken);
        var sources = new List<GitSourceDto>();
        string? firstError = null;
        var failed = 0;

        foreach (var app in apps)
        {
            var installations = await _gateway.ListInstallationsAsync(app, useCache: true, cancellationToken);
            if (!installations.IsSuccess)
            {
                failed++;
                firstError ??= $"{app.Name}: {FailureText(installations)}";
                _logger.LogWarning("GitHub App kurulumları alınamadı. Uygulama: {AppId}, Neden: {Reason}", app.AppId, FailureText(installations));
                continue;
            }

            sources.AddRange(installations.Data!
                .Where(installation => !installation.IsSuspended)
                .Select(installation => new GitSourceDto(
                    GitHubSourceIds.Format(app.Id, installation.Id),
                    $"{installation.AccountLogin} · {app.Name}")));
        }

        return apps.Count > 0 && failed == apps.Count
            ? ServiceResult<IReadOnlyList<GitSourceDto>>.Failure(firstError ?? "GitHub kurulumları alınamadı.")
            : ServiceResult<IReadOnlyList<GitSourceDto>>.Success(sources);
    }

    public async Task<ServiceResult<IReadOnlyList<GitRepositoryDto>>> ListRepositoriesAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var source = await ResolveAsync(sourceId, cancellationToken);
        if (source.App is null)
            return ServiceResult<IReadOnlyList<GitRepositoryDto>>.NotFound(SourceNotFoundMessage);

        var repositories = await _gateway.ListRepositoriesAsync(source.App, source.InstallationId, cancellationToken);
        return repositories.IsSuccess
            ? ServiceResult<IReadOnlyList<GitRepositoryDto>>.Success(repositories.Data!.Select(ToDto).ToList())
            : ServiceResult<IReadOnlyList<GitRepositoryDto>>.Failure(FailureText(repositories), repositories.ErrorType);
    }

    public async Task<ServiceResult<GitRepositoryDto>> GetRepositoryAsync(string sourceId, string repository, CancellationToken cancellationToken = default)
    {
        var source = await ResolveAsync(sourceId, cancellationToken);
        if (source.App is null)
            return ServiceResult<GitRepositoryDto>.NotFound(SourceNotFoundMessage);

        var found = await FindRepositoryAsync(source.App, source.InstallationId, repository, cancellationToken);
        return found.IsSuccess
            ? ServiceResult<GitRepositoryDto>.Success(ToDto(found.Data!))
            : ServiceResult<GitRepositoryDto>.Failure(FailureText(found), found.ErrorType);
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> ListBranchesAsync(string sourceId, string repository, CancellationToken cancellationToken = default)
    {
        var source = await ResolveAsync(sourceId, cancellationToken);
        if (source.App is null)
            return ServiceResult<IReadOnlyList<string>>.NotFound(SourceNotFoundMessage);

        var found = await FindRepositoryAsync(source.App, source.InstallationId, repository, cancellationToken);
        if (!found.IsSuccess)
            return ServiceResult<IReadOnlyList<string>>.Failure(FailureText(found), found.ErrorType);

        return await _gateway.ListBranchesAsync(source.App, source.InstallationId, found.Data!.FullName, cancellationToken);
    }

    public async Task<ServiceResult<GitAccessToken>> CreateAccessTokenAsync(string sourceId, string repository, CancellationToken cancellationToken = default)
    {
        var source = await ResolveAsync(sourceId, cancellationToken);
        if (source.App is null)
            return ServiceResult<GitAccessToken>.NotFound(SourceNotFoundMessage);

        var token = await _gateway.CreateRepositoryTokenAsync(source.App, source.InstallationId, repository, cancellationToken);
        return token.IsSuccess
            ? ServiceResult<GitAccessToken>.Success(new GitAccessToken(TokenUsername, token.Data!.Token))
            : ServiceResult<GitAccessToken>.Failure(FailureText(token), token.ErrorType);
    }

    /// <summary>
    /// Depo kurulumun erişebildiği listede olmalıdır; herkese açık ama kuruluma eklenmemiş bir depo
    /// GitHub API'de görünse bile deployment anahtarı alınamayacağı için kabul edilmez.
    /// </summary>
    private async Task<ServiceResult<GitHubRepositoryInfo>> FindRepositoryAsync(GitHubApp app, long installationId, string repository, CancellationToken cancellationToken)
    {
        var repositories = await _gateway.ListRepositoriesAsync(app, installationId, cancellationToken);
        if (!repositories.IsSuccess)
            return ServiceResult<GitHubRepositoryInfo>.Failure(FailureText(repositories), repositories.ErrorType);

        var found = repositories.Data!.FirstOrDefault(r => string.Equals(r.FullName, repository, StringComparison.OrdinalIgnoreCase));
        return found is null
            ? ServiceResult<GitHubRepositoryInfo>.NotFound($"'{repository}' deposu bu GitHub kurulumunda yok; GitHub'da kuruluma depo erişimi verin.")
            : ServiceResult<GitHubRepositoryInfo>.Success(found);
    }

    private async Task<(GitHubApp? App, long InstallationId)> ResolveAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (!GitHubSourceIds.TryParse(sourceId, out var appRecordId, out var installationId))
            return (null, 0);

        var app = await _repository.GetAsync(appRecordId, cancellationToken);
        return (app, installationId);
    }

    private static GitRepositoryDto ToDto(GitHubRepositoryInfo repository) =>
        new(repository.FullName, repository.CloneUrl, repository.DefaultBranch, repository.IsPrivate, repository.HtmlUrl);

    private static string FailureText(ServiceResult result) =>
        result.Errors.FirstOrDefault()?.Message ?? result.Message ?? "GitHub isteği başarısız.";
}
