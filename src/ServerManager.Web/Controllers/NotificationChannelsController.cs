using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.AlertManage)]
public class NotificationChannelsController : Controller
{
    public const string ProviderKey = "NotificationProvider";
    public const int RecentDeliveryCount = 20;

    private readonly INotificationChannelService _channelService;

    public NotificationChannelsController(INotificationChannelService channelService)
    {
        _channelService = channelService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new NotificationChannelIndexViewModel
        {
            Channels = await _channelService.GetChannelsAsync(cancellationToken),
            Deliveries = await _channelService.GetRecentDeliveriesAsync(RecentDeliveryCount, cancellationToken),
            Providers = _channelService.GetProviders()
        };
        return Request.IsAjax() ? PartialView("_ChannelList", model) : View(model);
    }

    [HttpGet]
    public IActionResult Create(string? provider)
    {
        var descriptor = _channelService.GetProviders().FirstOrDefault(p => p.SystemName == provider);
        if (descriptor is null)
            return RedirectToAction(nameof(Index));

        ViewData[ProviderKey] = descriptor;
        return View(new NotificationChannelFormDto { ProviderSystemName = descriptor.SystemName, Name = descriptor.DisplayName });
    }

    [HttpPost]
    public async Task<IActionResult> Create(NotificationChannelFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _channelService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kanal eklenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _channelService.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        var descriptor = _channelService.GetProviders().FirstOrDefault(p => p.SystemName == result.Data!.ProviderSystemName);
        if (descriptor is null)
            return RedirectToAction(nameof(Index));

        ViewData[ProviderKey] = descriptor;
        return View(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, NotificationChannelFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _channelService.UpdateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kanal güncellenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _channelService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kanal silinemedi.");

        return this.ApiSuccess(result.Message);
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.AlertingAction)]
    public async Task<IActionResult> Test(Guid id, CancellationToken cancellationToken)
    {
        var result = await _channelService.SendTestAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Test bildirimi gönderilemedi.");

        return this.ApiSuccess(result.Message);
    }
}
