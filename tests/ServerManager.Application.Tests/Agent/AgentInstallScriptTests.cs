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
        Assert.Contains("collect 2>/dev/null | curl", script);
    }

    [Fact]
    public void Agent_reads_the_token_header_from_a_root_only_file_instead_of_argv()
    {
        var script = AgentInstallScript.Build(Collect);
        var agent = script[script.IndexOf("<<'AGENT'", StringComparison.Ordinal)..script.IndexOf("\nAGENT\n", StringComparison.Ordinal)];

        Assert.Contains($"-H @{AgentInstallScript.HeaderFilePath}", agent);
        Assert.DoesNotContain("SM_TOKEN", agent);
        Assert.DoesNotContain("Bearer", agent);
        Assert.Contains("printf 'Authorization: Bearer %s\\n' \"$SM_TOKEN\" > \"$HEADER_FILE\"", script);
        Assert.Contains("chmod 600 \"$ENV_FILE\" \"$HEADER_FILE\"", script);
        Assert.Contains("umask 077", script);
        Assert.Contains("rm -f \"$BIN\" \"$ENV_FILE\" \"$HEADER_FILE\"", script);
    }

    [Fact]
    public void Install_requires_https_unless_plain_http_is_allowed_explicitly()
    {
        var script = AgentInstallScript.Build(Collect);

        Assert.Contains("grep -Eq '^https://[A-Za-z0-9.:/_-]+$'", script);
        Assert.Contains("if [ \"${SM_ALLOW_HTTP:-}\" = \"1\" ]; then", script);
    }
}
