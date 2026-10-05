using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.ServerView)]
public class AgentController : Controller
{
    private readonly IAgentService _agentService;

    public AgentController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    private string PanelBaseUrl => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

    private string InstallScriptUrl => PanelBaseUrl + "/api/agent/install.sh";

    [HttpGet]
    public async Task<IActionResult> Panel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _agentService.GetStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        return PartialView("_AgentPanel", new AgentPanelViewModel
        {
            Status = result.Data!,
            UninstallCommand = $"curl -fsSL {InstallScriptUrl} | sudo sh -s uninstall",
            IsHttps = Request.IsHttps,
            CanManage = User.HasPermission(Permissions.ServerEdit)
        });
    }

    [HttpPost]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> CreateToken(Guid id, CancellationToken cancellationToken)
    {
        var result = await _agentService.CreateTokenAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Token oluşturulamadı.");

        // Betik düz http adresi yalnızca açık onayla kabul eder; panel https değilse onay komuta eklenir (panel uyarıyı ayrıca gösterir).
        var allowHttp = Request.IsHttps ? string.Empty : "SM_ALLOW_HTTP=1 ";
        var command = $"curl -fsSL {InstallScriptUrl} | sudo {allowHttp}SM_URL={PanelBaseUrl} SM_TOKEN={result.Data!.Token} sh";
        return Ok(ApiResponse<AgentTokenResponse>.Success(new AgentTokenResponse(command), result.Message ?? "Token oluşturuldu."));
    }

    [HttpPost]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> RevokeToken(Guid id, CancellationToken cancellationToken)
    {
        var result = await _agentService.RevokeTokenAsync(id, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Token iptal edilemedi.");
    }
}
