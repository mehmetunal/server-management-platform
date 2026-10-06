using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;

namespace ServerManager.Web.Api;

public static class ApiKeyDefaults
{
    public const string Scheme = "ApiKey";
    public const string PathPrefix = "/api/v1";
    public const string RateLimitPolicy = "api-v1";
    public const string KeyIdClaim = "sm:api_key_id";
    public const string KeyNameClaim = "sm:api_key_name";
    internal const string FailureItemKey = "sm:api_key_failure";
}

/// <summary>
/// <c>Authorization: Bearer smk_…</c> ile kimlik doğrulama; yalnızca <c>/api/v1</c> uçlarında kullanılır. Oluşan kimlikte rol
/// claim'i yoktur (SuperAdmin atlaması anahtar kapsamını aşamasın); izinler anahtarın kapsamı ile kullanıcının o anki
/// izinlerinin kesişimidir. Hatalar ProblemDetails (application/problem+json) olarak döner.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string BearerPrefix = "Bearer ";

    private readonly IApiKeyService _apiKeys;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeys)
        : base(options, logger, encoder)
    {
        _apiKeys = apiKeys;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadBearerToken(Request);
        if (token is null)
            return AuthenticateResult.NoResult();

        var result = await _apiKeys.AuthenticateAsync(token, Context.Connection.RemoteIpAddress, Context.RequestAborted);
        if (!result.Succeeded)
        {
            Context.Items[ApiKeyDefaults.FailureItemKey] = result.FailureReason;
            return AuthenticateResult.Fail(result.FailureReason ?? "Geçersiz API anahtarı.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, result.UserName ?? result.UserId.ToString()),
            new(ApiKeyDefaults.KeyIdClaim, result.KeyId.ToString()),
            new(ApiKeyDefaults.KeyNameClaim, result.KeyName ?? string.Empty)
        };
        claims.AddRange((result.Permissions ?? new HashSet<string>()).Select(p => new Claim(Permissions.ClaimType, p)));

        var identity = new ClaimsIdentity(claims, ApiKeyDefaults.Scheme, ClaimTypes.Name, ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyDefaults.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        var detail = Context.Items.TryGetValue(ApiKeyDefaults.FailureItemKey, out var reason) && reason is string text
            ? text
            : "Authorization: Bearer smk_… başlığıyla geçerli bir API anahtarı gönderin.";
        return WriteProblemAsync(StatusCodes.Status401Unauthorized, "Kimlik doğrulanamadı", detail);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(
            StatusCodes.Status403Forbidden,
            "Yetki yok",
            "API anahtarının kapsamı veya anahtar sahibinin izinleri bu işlem için yeterli değil.");

    internal static string? ReadBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = header[BearerPrefix.Length..].Trim();
        return token.Length == 0 ? null : token;
    }

    private Task WriteProblemAsync(int statusCode, string title, string detail)
    {
        Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = Request.Path
        };
        return Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", Context.RequestAborted);
    }
}
