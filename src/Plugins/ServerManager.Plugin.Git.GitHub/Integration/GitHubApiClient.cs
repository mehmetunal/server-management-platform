using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.DTOs;
using ServerManager.Plugin.Git.GitHub.Services;

namespace ServerManager.Plugin.Git.GitHub.Integration;

public sealed class GitHubApiClient : IGitHubApiClient
{
    public const string HttpClientName = "GitHub";
    private const string ApiVersion = "2022-11-28";
    private const int MaxResponseBytes = 8 * 1024 * 1024;
    private const int PageSize = 100;
    private const int MaxPages = 10;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubApiClient> _logger;

    public GitHubApiClient(IHttpClientFactory httpClientFactory, IOptions<GitHubOptions> options, ILogger<GitHubApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<GitHubAppInfo>> GetAppAsync(string jwt, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, "/app", jwt, null, cancellationToken);
        return Read(response, GitHubJsonParser.ParseApp, "GitHub App bilgisi okunamadı.", AppNotFoundMessage);
    }

    public async Task<ServiceResult<IReadOnlyList<GitHubInstallationInfo>>> ListInstallationsAsync(string jwt, CancellationToken cancellationToken = default)
    {
        var installations = new List<GitHubInstallationInfo>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await SendAsync(HttpMethod.Get, $"/app/installations?per_page={PageSize}&page={page}", jwt, null, cancellationToken);
            var result = Read(response, GitHubJsonParser.ParseInstallations, "GitHub kurulum listesi okunamadı.", AppNotFoundMessage);
            if (!result.IsSuccess)
                return result;

            installations.AddRange(result.Data!);
            if (result.Data!.Count < PageSize)
                break;
        }

        return ServiceResult<IReadOnlyList<GitHubInstallationInfo>>.Success(installations);
    }

    public async Task<ServiceResult<GitHubInstallationToken>> CreateInstallationTokenAsync(
        string jwt,
        long installationId,
        string? repositoryName,
        CancellationToken cancellationToken = default)
    {
        string? body = repositoryName is null
            ? null
            : JsonSerializer.Serialize(new
            {
                repositories = new[] { repositoryName },
                permissions = new { contents = "read", metadata = "read" }
            });

        var response = await SendAsync(HttpMethod.Post, $"/app/installations/{installationId}/access_tokens", jwt, body, cancellationToken);
        if (response.Error is null && response.StatusCode == HttpStatusCode.UnprocessableEntity)
            return ServiceResult<GitHubInstallationToken>.Failure("Depo bu GitHub kurulumuna erişilebilir değil; kurulumda depoya izin verildiğini kontrol edin.");

        return Read(response, GitHubJsonParser.ParseToken, "GitHub erişim anahtarı okunamadı.", "GitHub kurulumu bulunamadı; uygulama hesaptan kaldırılmış olabilir.");
    }

    public async Task<ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>> ListRepositoriesAsync(string token, CancellationToken cancellationToken = default)
    {
        var repositories = new List<GitHubRepositoryInfo>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await SendAsync(HttpMethod.Get, $"/installation/repositories?per_page={PageSize}&page={page}", token, null, cancellationToken);
            var result = Read(response, GitHubJsonParser.ParseRepositoryPage, "GitHub depo listesi okunamadı.", "GitHub kurulumu bulunamadı.");
            if (!result.IsSuccess)
                return ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>.Failure(FailureText(result, "GitHub depo listesi okunamadı."), result.ErrorType);

            var items = result.Data!.Repositories;
            repositories.AddRange(items);
            if (items.Count < PageSize || repositories.Count >= result.Data.TotalCount)
                break;
        }

        return ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>.Success(repositories
            .OrderBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> ListBranchesAsync(string token, string fullName, CancellationToken cancellationToken = default)
    {
        var branches = new List<string>();
        var path = $"/repos/{GitHubUrls.RepositoryPath(fullName)}/branches";
        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await SendAsync(HttpMethod.Get, $"{path}?per_page={PageSize}&page={page}", token, null, cancellationToken);
            var result = Read(response, GitHubJsonParser.ParseBranches, "GitHub dal listesi okunamadı.", "Depo bulunamadı veya bu kurulumun erişimi yok.");
            if (!result.IsSuccess)
                return result;

            branches.AddRange(result.Data!);
            if (result.Data!.Count < PageSize)
                break;
        }

        return ServiceResult<IReadOnlyList<string>>.Success(branches);
    }

    public async Task<ServiceResult<GitHubManifestConversion>> ConvertManifestAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/app-manifests/{Uri.EscapeDataString(code)}/conversions", null, null, cancellationToken);
        return Read(response, GitHubJsonParser.ParseConversion, "GitHub uygulama bilgileri okunamadı.",
            "GitHub kodu geçersiz veya süresi dolmuş (kod bir saat geçerlidir); uygulamayı yeniden oluşturun.");
    }

    private const string AppNotFoundMessage = "GitHub App bulunamadı; uygulama silinmiş olabilir.";

    private static string FailureText(ServiceResult result, string fallback) =>
        result.Errors.FirstOrDefault()?.Message ?? result.Message ?? fallback;

    private static ServiceResult<T> Read<T>(GitHubHttpResponse response, Func<string, T?> parse, string unreadableMessage, string notFoundMessage)
    {
        var failure = ToFailure<T>(response, notFoundMessage);
        if (failure is not null)
            return failure;

        var value = parse(response.Body);
        return value is null
            ? ServiceResult<T>.Failure(unreadableMessage)
            : ServiceResult<T>.Success(value);
    }

    private static ServiceResult<T>? ToFailure<T>(GitHubHttpResponse response, string notFoundMessage)
    {
        if (response.Error is not null)
            return ServiceResult<T>.Failure(response.Error);

        return response.StatusCode switch
        {
            >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices => null,
            HttpStatusCode.Unauthorized =>
                ServiceResult<T>.Failure("GitHub kimlik doğrulaması başarısız; uygulama kimliği veya özel anahtar geçersiz ya da iptal edilmiş.", ServiceErrorType.Forbidden),
            HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests =>
                ServiceResult<T>.Failure("GitHub isteği reddetti (yetki yok veya istek sınırı aşıldı); biraz sonra yeniden deneyin.", ServiceErrorType.Forbidden),
            HttpStatusCode.NotFound => ServiceResult<T>.Failure(notFoundMessage, ServiceErrorType.NotFound),
            >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest =>
                ServiceResult<T>.Failure("GitHub API başka bir adrese yönlendiriyor; API adresini kontrol edin."),
            _ => ServiceResult<T>.Failure($"GitHub API HTTP {(int)response.StatusCode} döndürdü.")
        };
    }

    private async Task<GitHubHttpResponse> SendAsync(HttpMethod method, string pathAndQuery, string? bearer, string? jsonBody, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_options.ApiUrl.TrimEnd('/') + pathAndQuery, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return GitHubHttpResponse.Failed("GitHub API adresi geçersiz.");

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", ApiVersion);
        request.Headers.UserAgent.ParseAdd("ServerManager/1.0");
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        else if (method == HttpMethod.Post)
            request.Content = new StringContent(string.Empty);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await ReadBodyAsync(response, cancellationToken);
            return body is null
                ? GitHubHttpResponse.Failed("GitHub yanıtı çok büyük.")
                : new GitHubHttpResponse(response.StatusCode, body, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GitHubHttpResponse.Failed("GitHub zamanında yanıt vermedi.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "GitHub isteği başarısız. Host: {Host}", uri.Host);
            return GitHubHttpResponse.Failed(DescribeConnectionError(ex));
        }
    }

    private static async Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxResponseBytes)
                return null;

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static string DescribeConnectionError(HttpRequestException ex)
    {
        if (ex.InnerException is SocketException socket)
        {
            return socket.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => "GitHub API bağlantıyı reddetti.",
                SocketError.HostNotFound or SocketError.NoData => "GitHub API adresi çözümlenemedi.",
                _ => "GitHub API'ye ulaşılamıyor (ağ/güvenlik duvarı)."
            };
        }

        return ex.HttpRequestError switch
        {
            HttpRequestError.SecureConnectionError => "GitHub ile TLS bağlantısı kurulamadı.",
            HttpRequestError.NameResolutionError => "GitHub API adresi çözümlenemedi.",
            _ => "GitHub API'ye bağlanılamadı."
        };
    }
}
