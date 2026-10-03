using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Domain;
using ServerManager.Plugin.Git.GitHub.DTOs;

namespace ServerManager.Plugin.Git.GitHub.Services;

public sealed class GitHubAppGateway : IGitHubAppGateway
{
    private const string KeyUnreadableMessage = "GitHub App özel anahtarı çözülemedi; uygulamayı kaldırıp yeniden ekleyin.";
    private static readonly TimeSpan TokenRenewMargin = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _generations = new();
    private readonly IGitHubApiClient _apiClient;
    private readonly ISecretProtector _secretProtector;
    private readonly IMemoryCache _cache;
    private readonly GitHubOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GitHubAppGateway> _logger;

    public GitHubAppGateway(
        IGitHubApiClient apiClient,
        ISecretProtector secretProtector,
        IMemoryCache cache,
        IOptions<GitHubOptions> options,
        TimeProvider timeProvider,
        ILogger<GitHubAppGateway> logger)
    {
        _apiClient = apiClient;
        _secretProtector = secretProtector;
        _cache = cache;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private TimeSpan ListCacheDuration => TimeSpan.FromSeconds(Math.Clamp(_options.ListCacheSeconds, 0, 3600));

    public Task<ServiceResult<GitHubAppInfo>> VerifyAsync(long appId, string privateKeyPem, CancellationToken cancellationToken = default) =>
        _apiClient.GetAppAsync(GitHubJwt.Create(appId, privateKeyPem, _timeProvider.GetUtcNow()), cancellationToken);

    public async Task<ServiceResult<IReadOnlyList<GitHubInstallationInfo>>> ListInstallationsAsync(GitHubApp app, bool useCache, CancellationToken cancellationToken = default)
    {
        var key = $"github:installations:{app.Id:N}";
        if (useCache && _cache.TryGetValue(key, out IReadOnlyList<GitHubInstallationInfo>? cached) && cached is not null)
            return ServiceResult<IReadOnlyList<GitHubInstallationInfo>>.Success(cached);

        var jwt = CreateJwt(app);
        if (jwt is null)
            return ServiceResult<IReadOnlyList<GitHubInstallationInfo>>.Failure(KeyUnreadableMessage);

        var result = await _apiClient.ListInstallationsAsync(jwt, cancellationToken);
        if (result.IsSuccess)
            Store(app.Id, key, result.Data!, ListCacheDuration);

        return result;
    }

    public async Task<ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>> ListRepositoriesAsync(GitHubApp app, long installationId, CancellationToken cancellationToken = default)
    {
        var key = $"github:repositories:{app.Id:N}:{installationId}";
        if (_cache.TryGetValue(key, out IReadOnlyList<GitHubRepositoryInfo>? cached) && cached is not null)
            return ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>.Success(cached);

        var token = await GetInstallationTokenAsync(app, installationId, cancellationToken);
        if (!token.IsSuccess)
            return ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>.Failure(FailureText(token), token.ErrorType);

        var result = await _apiClient.ListRepositoriesAsync(token.Data!, cancellationToken);
        if (result.IsSuccess)
            Store(app.Id, key, result.Data!, ListCacheDuration);

        return result;
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> ListBranchesAsync(GitHubApp app, long installationId, string fullName, CancellationToken cancellationToken = default)
    {
        var token = await GetInstallationTokenAsync(app, installationId, cancellationToken);
        return token.IsSuccess
            ? await _apiClient.ListBranchesAsync(token.Data!, fullName, cancellationToken)
            : ServiceResult<IReadOnlyList<string>>.Failure(FailureText(token), token.ErrorType);
    }

    public Task<ServiceResult<GitHubInstallationToken>> CreateRepositoryTokenAsync(GitHubApp app, long installationId, string fullName, CancellationToken cancellationToken = default)
    {
        var jwt = CreateJwt(app);
        return jwt is null
            ? Task.FromResult(ServiceResult<GitHubInstallationToken>.Failure(KeyUnreadableMessage))
            : _apiClient.CreateInstallationTokenAsync(jwt, installationId, GitHubUrls.RepositoryName(fullName), cancellationToken);
    }

    public void Invalidate(Guid appRecordId)
    {
        if (_generations.TryRemove(appRecordId, out var generation))
        {
            generation.Cancel();
            generation.Dispose();
        }
    }

    /// <summary>Listeleme için kurulum genelindeki anahtar; süresi bitmeden 5 dk önce yenilenir.</summary>
    private async Task<ServiceResult<string>> GetInstallationTokenAsync(GitHubApp app, long installationId, CancellationToken cancellationToken)
    {
        var key = $"github:token:{app.Id:N}:{installationId}";
        if (_cache.TryGetValue(key, out string? cached) && cached is not null)
            return ServiceResult<string>.Success(cached);

        var jwt = CreateJwt(app);
        if (jwt is null)
            return ServiceResult<string>.Failure(KeyUnreadableMessage);

        var result = await _apiClient.CreateInstallationTokenAsync(jwt, installationId, null, cancellationToken);
        if (!result.IsSuccess)
            return ServiceResult<string>.Failure(FailureText(result), result.ErrorType);

        var lifetime = result.Data!.ExpiresAt - _timeProvider.GetUtcNow() - TokenRenewMargin;
        if (lifetime > TimeSpan.Zero)
            Store(app.Id, key, result.Data.Token, lifetime);

        return ServiceResult<string>.Success(result.Data.Token);
    }

    private string? CreateJwt(GitHubApp app)
    {
        try
        {
            return GitHubJwt.Create(app.AppId, _secretProtector.Unprotect(app.EncryptedPrivateKey), _timeProvider.GetUtcNow());
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException or FormatException)
        {
            _logger.LogWarning("GitHub App özel anahtarı çözülemedi. Uygulama: {AppId}", app.AppId);
            return null;
        }
    }

    private void Store<T>(Guid appRecordId, string key, T value, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return;

        var generation = _generations.GetOrAdd(appRecordId, _ => new CancellationTokenSource());
        var options = new MemoryCacheEntryOptions().SetAbsoluteExpiration(duration);
        try
        {
            options.AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(generation.Token));
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _cache.Set(key, value, options);
    }

    private static string FailureText(ServiceResult result) =>
        result.Errors.FirstOrDefault()?.Message ?? result.Message ?? "GitHub isteği başarısız.";
}
