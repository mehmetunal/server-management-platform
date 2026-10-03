using System.Diagnostics;
using ServerManager.Web.Extensions;
using ServerManager.Web.Models;

namespace ServerManager.Web.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("İstek istemci tarafından iptal edildi: {Path}", context.Request.Path);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            _logger.LogError(ex, "İşlenmeyen hata. TraceId: {TraceId}, Path: {Path}", traceId, context.Request.Path);

            if (!context.Request.WantsJson() || context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                $"Beklenmeyen bir hata oluştu. Destek için referans: {traceId}",
                StatusCodes.Status500InternalServerError));
        }
    }
}
