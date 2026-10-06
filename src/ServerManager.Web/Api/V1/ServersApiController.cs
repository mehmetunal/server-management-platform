using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Framework.Authorization;

namespace ServerManager.Web.Api.V1;

/// <summary>Anahtarın kimliği ve etkin izinleri.</summary>
[Route("api/v1/me")]
public sealed class MeApiController : ApiV1ControllerBase
{
    [HttpGet]
    public ActionResult<ApiMe> Get() => new ApiMe(
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        User.Identity?.Name ?? string.Empty,
        Guid.Parse(User.FindFirstValue(ApiKeyDefaults.KeyIdClaim)!),
        User.FindFirstValue(ApiKeyDefaults.KeyNameClaim) ?? string.Empty,
        User.FindAll(Permissions.ClaimType).Select(c => c.Value).Order(StringComparer.Ordinal).ToList());
}

[Route("api/v1/servers")]
[HasPermission(Permissions.ServerView)]
public sealed class ServersApiController : ApiV1ControllerBase
{
    private readonly IServerService _servers;
    private readonly IMonitoringService _monitoring;

    public ServersApiController(IServerService servers, IMonitoringService monitoring)
    {
        _servers = servers;
        _monitoring = monitoring;
    }

    /// <summary>Sunucu listesi. <paramref name="status"/>: Unknown, Online, Offline, Warning … (büyük/küçük harf duyarsız).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiPage<ApiServer>>> List(string? search, string? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var filter = new ServerFilterDto { Search = search, Page = NormalizePage(page), PageSize = NormalizePageSize(pageSize) };
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ServerStatus>(status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                return Problem(detail: $"Geçersiz durum: {status}", statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek");
            filter.Status = parsed;
        }

        var result = await _servers.SearchAsync(filter, cancellationToken);
        return ApiPage<ApiServer>.From(result, ApiServer.From);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiServer>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _servers.GetDetailsAsync(id, cancellationToken);
        return result.IsSuccess ? ApiServer.From(result.Data!) : Problem(result, "Sunucu bulunamadı");
    }

    /// <summary>Sunucunun durumu ve son ölçümü (izleme açıksa).</summary>
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<ApiServerStatus>> Status(Guid id, CancellationToken cancellationToken)
    {
        var result = await _servers.GetDetailsAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return Problem(result, "Sunucu bulunamadı");

        var server = result.Data!;
        var summaries = await _monitoring.GetLatestSummariesAsync([id], cancellationToken);
        var metrics = summaries.TryGetValue(id, out var summary) ? ApiServerMetrics.From(summary) : null;
        return new ApiServerStatus(server.Id, server.Name, server.Status.ToString(), server.LastSeenAt, server.MonitoringEnabled, metrics);
    }
}
