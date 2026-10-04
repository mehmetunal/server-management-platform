using ServerManager.Application.Security;
using ServerManager.Application.Tests.TestData;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Security;

namespace ServerManager.Application.Tests.Security;

public class SecurityAnalyzerTests
{
    private static SecurityReport Analyze(string sample) => SecurityAnalyzer.Analyze(SecurityFactsParser.Parse(sample));

    private static SecurityCheckStatus StatusOf(SecurityReport report, string key) =>
        Assert.Single(report.Findings, f => f.Key == key).Status;

    [Fact]
    public void Flags_risky_configuration_on_privileged_scan()
    {
        var report = Analyze(SecurityScanSamples.RootUbuntu);

        Assert.Equal(SecurityCheckStatus.Critical, StatusOf(report, "ssh.root_login"));
        Assert.Equal(SecurityCheckStatus.Warning, StatusOf(report, "ssh.password_auth"));
        Assert.Equal(SecurityCheckStatus.Pass, StatusOf(report, "ssh.empty_passwords"));
        Assert.Equal(SecurityCheckStatus.Info, StatusOf(report, "ssh.max_auth_tries"));
        Assert.Equal(SecurityCheckStatus.Info, StatusOf(report, "ssh.x11"));
        Assert.Equal(SecurityCheckStatus.Warning, StatusOf(report, "ssh.failed_logins"));
        Assert.Equal(SecurityCheckStatus.Critical, StatusOf(report, "ports.6379.tcp"));
        Assert.Equal(SecurityCheckStatus.Warning, StatusOf(report, "ports.5432.tcp"));
        Assert.DoesNotContain(report.Findings, f => f.Key == "ports.3306.tcp");
        Assert.Equal(SecurityCheckStatus.Critical, StatusOf(report, "firewall.status"));
        Assert.Equal(SecurityCheckStatus.Critical, StatusOf(report, "docker.tcp"));
        Assert.Equal(SecurityCheckStatus.Info, StatusOf(report, "docker.published"));
        Assert.Equal(SecurityCheckStatus.Warning, StatusOf(report, "updates.pending"));
        Assert.Equal(SecurityCheckStatus.Warning, StatusOf(report, "updates.reboot"));
        Assert.Equal(SecurityCheckStatus.Info, StatusOf(report, "updates.auto"));
        Assert.Equal(SecurityCheckStatus.Critical, StatusOf(report, "users.uid0"));
        Assert.Equal(SecurityCheckStatus.Critical, StatusOf(report, "users.empty_password"));
        Assert.Equal(SecurityCheckStatus.Info, StatusOf(report, "disk.encryption"));

        Assert.Equal(6, report.CriticalCount);
        Assert.Equal(5, report.WarningCount);
        Assert.Equal(0, report.Score);
    }

    [Fact]
    public void Marks_unreadable_checks_as_unknown_on_restricted_scan()
    {
        var report = Analyze(SecurityScanSamples.RestrictedAlpine);

        Assert.Equal(SecurityCheckStatus.Pass, StatusOf(report, "ssh.root_login"));
        Assert.Equal(SecurityCheckStatus.Pass, StatusOf(report, "ssh.password_auth"));
        Assert.Equal(SecurityCheckStatus.Unknown, StatusOf(report, "ssh.failed_logins"));
        Assert.Equal(SecurityCheckStatus.Unknown, StatusOf(report, "firewall.status"));
        Assert.Equal(SecurityCheckStatus.Unknown, StatusOf(report, "users.empty_password"));
        Assert.Equal(SecurityCheckStatus.Unknown, StatusOf(report, "disk.encryption"));
        Assert.Equal(SecurityCheckStatus.Pass, StatusOf(report, "updates.pending"));
        Assert.Equal(SecurityCheckStatus.Pass, StatusOf(report, "ports.risky"));
        Assert.DoesNotContain(report.Findings, f => f.Category == SecurityAnalyzer.DockerCategory);
        Assert.Equal(100, report.Score);
    }

    [Fact]
    public void Root_login_with_keys_only_is_a_warning()
    {
        var facts = new SecurityFacts
        {
            IsRoot = true,
            Ssh = new SshFacts
            {
                Source = SshFacts.EffectiveSource,
                Settings = new Dictionary<string, string> { ["permitrootlogin"] = "yes", ["passwordauthentication"] = "no" }
            }
        };

        var report = SecurityAnalyzer.Analyze(facts);

        Assert.Equal(SecurityCheckStatus.Warning, StatusOf(report, "ssh.root_login"));
        Assert.Equal(SecurityCheckStatus.Pass, StatusOf(report, "ssh.password_auth"));
    }

    [Fact]
    public void Missing_firewall_is_a_warning_when_no_risky_port_is_exposed()
    {
        var facts = new SecurityFacts
        {
            IsRoot = true,
            PortsAvailable = true,
            Ports = [new ListeningPort { Protocol = "tcp", Address = "0.0.0.0", Port = 443, Exposure = PortExposure.AllInterfaces }]
        };

        var report = SecurityAnalyzer.Analyze(facts);

        var firewall = Assert.Single(report.Findings, f => f.Key == "firewall.status");
        Assert.Equal(SecurityCheckStatus.Warning, firewall.Status);
        Assert.Equal("Güvenlik duvarı kurulu değil", firewall.Title);
    }

    [Fact]
    public void Risky_port_behind_active_firewall_is_still_reported()
    {
        var facts = new SecurityFacts
        {
            IsRoot = true,
            PortsAvailable = true,
            Ports = [new ListeningPort { Protocol = "tcp", Address = "0.0.0.0", Port = 2375, Exposure = PortExposure.AllInterfaces }],
            Firewall = new FirewallFacts { Tools = [new FirewallTool { Name = "ufw", Active = true }] }
        };

        var report = SecurityAnalyzer.Analyze(facts);

        Assert.Equal(SecurityCheckStatus.Pass, StatusOf(report, "firewall.status"));
        var docker = Assert.Single(report.Findings, f => f.Key == "ports.2375.tcp");
        Assert.Equal(SecurityCheckStatus.Critical, docker.Status);
        Assert.Equal("sudo ufw deny 2375/tcp", docker.FixCommand);
    }

    [Theory]
    [InlineData(0, 0, 100)]
    [InlineData(1, 0, 75)]
    [InlineData(0, 3, 70)]
    [InlineData(2, 2, 30)]
    [InlineData(5, 0, 0)]
    public void Score_deducts_per_finding(int critical, int warning, int expected) =>
        Assert.Equal(expected, SecurityAnalyzer.Score(critical, warning));

    [Fact]
    public void Report_round_trips_through_json()
    {
        var report = Analyze(SecurityScanSamples.RootUbuntu);

        var restored = SecurityReportSerializer.Deserialize(SecurityReportSerializer.Serialize(report));

        Assert.NotNull(restored);
        Assert.Equal(report.Score, restored.Score);
        Assert.Equal(report.Findings.Count, restored.Findings.Count);
        Assert.Equal(report.Findings[0], restored.Findings[0]);
        Assert.Equal(report.Facts.Ports.Count, restored.Facts.Ports.Count);
        Assert.Equal("yes", restored.Facts.Ssh.Settings["permitrootlogin"]);
        Assert.False(restored.Facts.Firewall.AnyActive);
        Assert.Null(SecurityReportSerializer.Deserialize("{bozuk"));
    }
}
