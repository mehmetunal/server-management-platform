using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Common;
using ServerManager.Web.Models;

namespace ServerManager.Web.Extensions;

public static class ApiResultExtensions
{
    private const string InvalidFormMessage = "Lütfen formdaki hataları düzeltin.";

    public static IActionResult ApiSuccess(this ControllerBase controller, string? message, string? redirectUrl = null) =>
        controller.Ok(ApiResponse<string>.Success(redirectUrl, message ?? "İşlem tamamlandı."));

    public static IActionResult ApiFailure(this ControllerBase controller, ServiceResult result, string fallbackMessage)
    {
        var statusCode = StatusCodeFor(result.ErrorType);
        var errors = result.Errors
            .GroupBy(e => e.PropertyName ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
        var message = result.ErrorType == ServiceErrorType.Validation && errors.Keys.Any(k => k.Length > 0)
            ? result.Message ?? InvalidFormMessage
            : result.Errors.FirstOrDefault()?.Message ?? result.Message ?? fallbackMessage;

        return controller.StatusCode(statusCode, ApiResponse<string>.Fail(
            message,
            statusCode,
            result.Errors.Select(e => e.Message).ToList(),
            errors));
    }

    public static IActionResult ApiInvalidModel(this ControllerBase controller)
    {
        var errors = controller.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return controller.BadRequest(ApiResponse<string>.Fail(
            InvalidFormMessage,
            StatusCodes.Status400BadRequest,
            errors.Values.SelectMany(messages => messages).ToList(),
            errors));
    }

    public static int StatusCodeFor(ServiceErrorType errorType) => errorType switch
    {
        ServiceErrorType.NotFound => StatusCodes.Status404NotFound,
        ServiceErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ServiceErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
