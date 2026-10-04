using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Agent;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/agent")]
[EnableRateLimiting(RateLimitPolicies.AgentReport)]
public class AgentApiController : Controller
{
    private const string BearerPrefix = "Bearer ";

    private readonly IAgentService _agentService;

    public AgentApiController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    [HttpGet("install.sh")]
    public IActionResult InstallScript() =>
        Content(_agentService.BuildInstallScript(), "text/plain; charset=utf-8");

    [HttpPost("report")]
    [RequestSizeLimit(AgentRules.MaxReportBytes)]
    public async Task<IActionResult> Report(CancellationToken cancellationToken)
    {
        var header = Request.Headers.Authorization.ToString();
        var token = header.StartsWith(BearerPrefix, StringComparison.Ordinal) ? header[BearerPrefix.Length..].Trim() : null;

        string output;
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            output = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (BadHttpRequestException)
        {
            return Outcome(StatusCodes.Status413PayloadTooLarge, "Rapor çok büyük.");
        }

        var outcome = await _agentService.ReportAsync(token, Request.Headers["X-Agent-Version"].ToString(), output, cancellationToken);
        return outcome.Status switch
        {
            AgentReportStatus.Accepted => Outcome(StatusCodes.Status202Accepted, outcome.Message),
            AgentReportStatus.InvalidToken => Outcome(StatusCodes.Status401Unauthorized, outcome.Message),
            AgentReportStatus.TooFrequent => Outcome(StatusCodes.Status429TooManyRequests, outcome.Message),
            AgentReportStatus.MonitoringDisabled => Outcome(StatusCodes.Status409Conflict, outcome.Message),
            _ => Outcome(StatusCodes.Status400BadRequest, outcome.Message)
        };
    }

    private ObjectResult Outcome(int statusCode, string message) =>
        StatusCode(statusCode, new { isSuccess = statusCode < 300, message });
}
