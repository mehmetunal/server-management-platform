using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Interfaces.Monitoring;

namespace ServerManager.Infrastructure.Monitoring;

public sealed class AgentReportParser : IAgentReportParser
{
    public string CollectionScript => LinuxMetricsScript.Script.Replace("\r\n", "\n");

    public SystemMetricsSnapshot Parse(string output, DateTime nowUtc) => LinuxMetricsParser.Parse(output, nowUtc);
}
