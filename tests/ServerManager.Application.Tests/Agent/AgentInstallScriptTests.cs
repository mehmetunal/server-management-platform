using ServerManager.Application.Agent;

namespace ServerManager.Application.Tests.Agent;

public class AgentInstallScriptTests
{
    private const string Collect = "export LC_ALL=C\r\necho @@T1; cat /proc/uptime\r\necho @@END\r\n";

    [Fact]
    public void Build_embeds_collection_script_and_settings()
    {
        var script = AgentInstallScript.Build(Collect);

        Assert.StartsWith("#!/bin/sh\n", script);
        Assert.Contains("collect() {\nexport LC_ALL=C\necho @@T1; cat /proc/uptime\necho @@END\n}", script);
        Assert.Contains($"X-Agent-Version: {AgentRules.CurrentVersion}", script);
        Assert.Contains($"OnUnitActiveSec={AgentRules.ReportIntervalSeconds}", script);
        Assert.Contains($"BIN={AgentInstallScript.AgentPath}", script);
        Assert.Contains("/api/agent/report", script);
        Assert.DoesNotContain("\r", script);
        Assert.DoesNotContain("__", script);
    }

    [Fact]
    public void Build_quotes_agent_heredoc_so_variables_expand_at_runtime()
    {
        var script = AgentInstallScript.Build(Collect);

        Assert.Contains("cat > \"$BIN\" <<'AGENT'", script);
        Assert.Contains("-H \"Authorization: Bearer $SM_TOKEN\"", script);
    }
}
