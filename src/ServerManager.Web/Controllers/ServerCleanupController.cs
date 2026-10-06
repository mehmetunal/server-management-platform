using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.Cleanup;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

/// <summary>
/// Sunucu "Temizlik" sekmesi: kullanılmayan Docker kaynakları, paket önbelleği, journal, eski loglar, /tmp ve snap revizyonları.
/// Silme isteği satır satır NDJSON olarak akıtılır; böylece sonuç günlüğü işlem sürerken görünür.
/// </summary>
[HasPermission(Permissions.ServerCleanup)]
public class ServerCleanupController : Controller
{
    private const string ErrorView = "~/Views/ServerSystem/_SystemError.cshtml";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServerCleanupService _cleanupService;
    private readonly ServerPageBuilder _pageBuilder;

    public ServerCleanupController(IServerCleanupService cleanupService, ServerPageBuilder pageBuilder)
    {
        _cleanupService = cleanupService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, [FromQuery] CleanupOptions options, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.CleanupTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        options = CleanupRules.Normalize(options);
        return View(new ServerCleanupPageViewModel
        {
            Page = page,
            Options = options,
            PanelUrl = Url.Action(nameof(Scan), new { id, options.LogDays, options.TempDays, options.JournalMaxMegabytes })!
        });
    }

    [HttpGet]
    public async Task<IActionResult> Scan(Guid id, [FromQuery] CleanupOptions options, CancellationToken cancellationToken)
    {
        var result = await _cleanupService.ScanAsync(id, options, cancellationToken);
        if (!result.IsSuccess)
        {
            Response.StatusCode = result.ErrorType == ServiceErrorType.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status200OK;
            return PartialView(ErrorView, result.Message ?? "Sunucu taranamadı.");
        }

        return PartialView("_Scan", new ServerCleanupPanelModel { ServerId = id, Scan = result.Data! });
    }

    /// <summary>
    /// Seçilen öğeleri siler (dryRun: yalnızca önizleme). Yanıt <c>application/x-ndjson</c>: her satır bir günlük kaydı
    /// (<c>{"type":"log",…}</c>), son satır özet (<c>{"type":"done",…}</c>). İstemci bağlantıyı kapatsa da işlem tamamlanır ve audit'e yazılır.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.SystemAction)]
    public async Task<IActionResult> Execute(Guid id, [FromForm] List<string>? keys, [FromForm] CleanupOptions options, [FromForm] bool dryRun)
    {
        if (keys is not { Count: > 0 })
            return this.ApiFailure(ServiceResult.Failure("Temizlenecek öğe seçilmedi."), "Temizlenecek öğe seçilmedi.");
        if (keys.Count > CleanupRules.MaxSelectedItems)
            return this.ApiFailure(ServiceResult.Failure($"Tek seferde en fazla {CleanupRules.MaxSelectedItems} öğe temizlenebilir."), "Çok fazla öğe seçildi.");

        var stream = new NdjsonStream(HttpContext);
        var result = await _cleanupService.ExecuteAsync(id, keys, options, dryRun,
            (entry, _) => stream.WriteAsync(new
            {
                type = "log",
                level = entry.Level.ToString().ToLowerInvariant(),
                message = entry.Message,
                output = entry.Output,
                key = entry.Key
            }),
            CancellationToken.None);

        if (!stream.Started && !result.IsSuccess)
            return this.ApiFailure(result, "Temizlik çalıştırılamadı.");

        await stream.WriteAsync(new
        {
            type = "done",
            isSuccess = result.IsSuccess,
            message = result.Message,
            data = result.Data
        });
        return new EmptyResult();
    }

    /// <summary>Yanıtı ilk yazımda başlatır; istemci ayrıldıysa yazma hataları yutulur (işlem sürer).</summary>
    private sealed class NdjsonStream(HttpContext context)
    {
        private bool _broken;

        public bool Started { get; private set; }

        public async Task WriteAsync(object payload)
        {
            if (_broken)
                return;

            try
            {
                if (!Started)
                {
                    Started = true;
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = "application/x-ndjson; charset=utf-8";
                    context.Response.Headers.CacheControl = "no-store";
                    context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                }

                await JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions, context.RequestAborted);
                await context.Response.Body.WriteAsync("\n"u8.ToArray(), context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
                _broken = true;
            }
        }
    }
}
