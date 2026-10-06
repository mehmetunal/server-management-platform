using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Common;
using ServerManager.Web.Framework.Mvc;

namespace ServerManager.Web.Api.V1;

/// <summary>
/// REST API v1 denetleyicilerinin tabanı: yalnızca API anahtarı (cookie değil), antiforgery yok (tarayıcı oturumu kabul
/// edilmediği için CSRF riski yok), anahtar başına hız sınırı, JSON (camelCase) ve ProblemDetails hataları.
/// Uçlardaki <c>[HasPermission]</c> paneldeki izinlerin aynısıdır.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme)]
[IgnoreAntiforgeryToken]
[EnableRateLimiting(ApiKeyDefaults.RateLimitPolicy)]
public abstract class ApiV1ControllerBase : ControllerBase
{
    /// <summary>Servis hatasını uygun durum koduyla ProblemDetails'e çevirir.</summary>
    protected ObjectResult Problem(ServiceResult result, string fallbackTitle)
    {
        var statusCode = ApiResultExtensions.StatusCodeFor(result.ErrorType);
        var detail = result.Errors.FirstOrDefault()?.Message ?? result.Message ?? fallbackTitle;
        if (result.ErrorType == ServiceErrorType.Validation && result.Errors.Count > 0)
        {
            var errors = result.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? string.Empty : char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
            var problem = new ValidationProblemDetails(errors) { Title = fallbackTitle, Detail = result.Message, Status = statusCode };
            return new ObjectResult(problem) { StatusCode = statusCode, ContentTypes = { "application/problem+json" } };
        }

        return Problem(detail: detail, statusCode: statusCode, title: fallbackTitle);
    }

    protected static int NormalizePageSize(int pageSize) => Paging.NormalizePageSize(pageSize);

    protected static int NormalizePage(int page) => Paging.NormalizePage(page);
}
