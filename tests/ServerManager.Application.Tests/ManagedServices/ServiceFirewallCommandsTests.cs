using ServerManager.Application.ManagedServices;
using ServerManager.Infrastructure.ManagedServices;

namespace ServerManager.Application.Tests.ManagedServices;

public class ServiceFirewallCommandsTests
{
    private static readonly ServiceFirewallPlan Plan = new("sm-svc-db", [15432, 19000], ["203.0.113.10/32", "10.0.0.0/24", "2001:db8::/32"]);

    [Fact]
    public void Rules_are_removed_by_tag_before_being_added()
    {
        var script = ServiceFirewallCommands.ApplyScript(Plan);

        var remove = script.IndexOf("sm_remove iptables", StringComparison.Ordinal);
        var firstInsert = script.IndexOf("iptables -I DOCKER-USER", StringComparison.Ordinal);
        Assert.True(remove >= 0 && remove < firstInsert);
        Assert.Contains("sm_remove ip6tables", script);
        Assert.Contains("awk -v t=\"/* $tag */\" 'index($0, t) { print $1 }' | sort -rn", script);
        Assert.Contains("-D DOCKER-USER \"$n\"", script);
    }

    [Fact]
    public void Each_port_gets_drop_and_allow_rules_matching_original_destination_port()
    {
        var script = ServiceFirewallCommands.ApplyScript(Plan);

        foreach (var port in Plan.Ports)
        {
            var match = $"-p tcp -m conntrack --ctstate DNAT --ctorigdstport {port} --ctdir ORIGINAL -m comment --comment 'sm-svc-db'";
            var drop = script.IndexOf($"iptables -I DOCKER-USER 1 {match} -j DROP", StringComparison.Ordinal);
            var allow = script.IndexOf($"iptables -I DOCKER-USER 1 -s '203.0.113.10/32' {match} -j RETURN", StringComparison.Ordinal);
            Assert.True(drop >= 0, $"DROP kuralı yok: {port}");
            Assert.True(allow > drop, "RETURN kuralı DROP'tan sonra eklenmeli (zincirde üstte kalır).");
            Assert.Contains($"iptables -I DOCKER-USER 1 -s '10.0.0.0/24' {match} -j RETURN", script);
        }
    }

    [Fact]
    public void Ipv6_sources_only_go_to_ip6tables()
    {
        var script = ServiceFirewallCommands.ApplyScript(Plan);
        var lines = script.Split('\n');

        Assert.Contains(lines, l => l.Trim().StartsWith("ip6tables -I DOCKER-USER 1 -s '2001:db8::/32'", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("iptables -I DOCKER-USER 1 -s '2001:db8::/32'", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Trim().StartsWith("ip6tables -I DOCKER-USER 1 -s '203.0.113.10/32'", StringComparison.Ordinal));
        Assert.Contains("if command -v ip6tables >/dev/null 2>&1 && ip6tables -L DOCKER-USER -n", script);
    }

    [Fact]
    public void Missing_chain_fails_instead_of_leaving_port_open()
    {
        var script = ServiceFirewallCommands.ApplyScript(Plan);
        Assert.Contains($"exit {ServiceFirewallCommands.FirewallUnavailableExitCode}", script);
    }

    [Fact]
    public void Script_is_deterministic_so_reapplying_is_idempotent()
    {
        Assert.Equal(ServiceFirewallCommands.ApplyScript(Plan), ServiceFirewallCommands.ApplyScript(Plan with { }));
    }

    [Fact]
    public void Without_allow_list_only_old_rules_are_removed()
    {
        var plan = ServiceFirewallPlan.None("sm-svc-db");
        var script = ServiceFirewallCommands.ApplyScript(plan);

        Assert.False(plan.HasRules);
        Assert.Contains("sm_remove iptables", script);
        Assert.DoesNotContain("-I DOCKER-USER", script);
        Assert.DoesNotContain("systemctl", ServiceFirewallCommands.Apply(plan, "db"));
        Assert.Contains("rm -f -- '\"'\"'/var/lib/sm-services/db/firewall.sh'\"'\"'", ServiceFirewallCommands.Apply(plan, "db"));
    }

    [Fact]
    public void Rules_are_persisted_with_systemd_unit()
    {
        var command = ServiceFirewallCommands.Apply(Plan, "db");

        Assert.Contains("cat > '\"'\"'/var/lib/sm-services/db/firewall.sh'\"'\"'", command);
        Assert.Contains(ServiceFirewallCommands.SystemdUnitPath, command);
        Assert.Contains("After=docker.service", command);
        Assert.Contains("systemctl enable", command);
    }

    [Fact]
    public void Tag_match_is_exact_to_avoid_touching_other_services()
    {
        var remove = ServiceFirewallCommands.Remove("db");
        var count = ServiceFirewallCommands.Count("db");

        Assert.Contains("tag='\"'\"'sm-svc-db'\"'\"'", remove);
        Assert.Contains("/* sm-svc-db */", count);
        Assert.Contains("SM_FW=", count);
        Assert.Equal(3, SshManagedServiceProvider.ParseFirewallCount("SM_FW=3\n"));
        Assert.Equal(0, SshManagedServiceProvider.ParseFirewallCount("garbage"));
    }
}
