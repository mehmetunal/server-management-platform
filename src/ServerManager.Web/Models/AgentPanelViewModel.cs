using ServerManager.Application.DTOs.Agent;

namespace ServerManager.Web.Models;

public class AgentPanelViewModel
{
    public required AgentStatusDto Status { get; init; }

    public required string UninstallCommand { get; init; }

    public bool IsHttps { get; init; }

    public bool CanManage { get; init; }
}
