using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using ServerManager.Application.ApiKeys;

namespace ServerManager.Web.Api;

/// <summary>
/// REST API v1 (<c>/api/v1</c>): API anahtarı kimlik doğrulaması, anahtar başına hız sınırı ve OpenAPI belgesi.
/// /api/v1 yalnızca "ApiKey" şemasını kabul eder; tarayıcı oturumu (cookie) bu uçlara erişemez, bu yüzden antiforgery
/// gerekmez. Tek istisna OpenAPI belgesidir (salt okunur GET): panelden açılabilmesi için cookie ile de okunabilir.
/// </summary>
public static class ApiV1Extensions
{
    public const string OpenApiPath = ApiKeyDefaults.PathPrefix + "/openapi.json";
    public const string DocumentName = "v1";

    /// <summary>Yalnızca OpenAPI belgesi için: Bearer başlığı varsa API anahtarı, yoksa panel oturumu. Yanıt hep 401 (yönlendirme yok).</summary>
    public const string DocumentScheme = "ApiKeyOrCookie";

    public static IServiceCollection AddApiV1(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, null)
            .AddPolicyScheme(DocumentScheme, null, options =>
            {
                options.ForwardDefaultSelector = context => ApiKeyAuthenticationHandler.ReadBearerToken(context.Request) is not null
                    ? ApiKeyDefaults.Scheme
                    : IdentityConstants.ApplicationScheme;
                options.ForwardChallenge = ApiKeyDefaults.Scheme;
                options.ForwardForbid = ApiKeyDefaults.Scheme;
            });

        // Hız sınırı kimlik doğrulamadan önce çalışır: bölüm anahtarın özetine göre (yoksa istemci IP'sine göre) seçilir.
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(ApiKeyDefaults.RateLimitPolicy, context =>
        {
            var permits = Math.Max(1, context.RequestServices.GetRequiredService<IOptionsMonitor<ApiKeyOptions>>().CurrentValue.RequestsPerMinute);
            return RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
        }));

        services.AddOpenApi(DocumentName, options =>
        {
            options.ShouldInclude = description =>
                description.RelativePath?.StartsWith("api/v1/", StringComparison.OrdinalIgnoreCase) == true;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Mag Server Manager API",
                    Version = "v1",
                    Description = "Kişisel API anahtarıyla (Authorization: Bearer smk_…) erişilen REST API. Her uç panelle aynı izni ister; "
                                  + "etkin izinler anahtarın kapsamı ile anahtar sahibinin o anki izinlerinin kesişimidir."
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[ApiKeyDefaults.Scheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "smk_<önek>_<gizli>",
                    Description = "Hesabım → API anahtarları sayfasında oluşturulan kişisel anahtar."
                };
                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(ApiKeyDefaults.Scheme, document)] = []
                });
                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>/api/v1 yanıtları HTML hata sayfasına (status code pages) çevrilmez; boş 404/405 yerine ProblemDetails döner.</summary>
    public static IApplicationBuilder UseApiV1StatusCodes(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(ApiKeyDefaults.PathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var feature = context.Features.Get<IStatusCodePagesFeature>();
                if (feature is not null)
                    feature.Enabled = false;

                await next(context);

                if (context.Response is { HasStarted: false, StatusCode: >= 400 } response
                    && response.ContentLength is null
                    && string.IsNullOrEmpty(response.ContentType))
                {
                    await response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = response.StatusCode,
                        Title = response.StatusCode switch
                        {
                            StatusCodes.Status404NotFound => "Bulunamadı",
                            StatusCodes.Status405MethodNotAllowed => "Yöntem desteklenmiyor",
                            StatusCodes.Status429TooManyRequests => "Çok fazla istek",
                            _ => "İstek başarısız"
                        },
                        Instance = context.Request.Path
                    }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", context.RequestAborted);
                }

                return;
            }

            await next(context);
        });

    public static IEndpointRouteBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOpenApi(OpenApiPath)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = DocumentScheme
            })
            .RequireRateLimiting(ApiKeyDefaults.RateLimitPolicy);
        return endpoints;
    }

    private static string PartitionKey(HttpContext context)
    {
        var token = ApiKeyAuthenticationHandler.ReadBearerToken(context.Request);
        if (token is not null)
            return "key:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)), 0, 12);

        return "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
